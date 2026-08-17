using System.Windows.Threading;

namespace CalendarBar;

public sealed class MailSyncService : ObservableObject
{
    public static MailSyncService Shared { get; } = new();

    private List<MailMessage> _messages = [];
    private SyncState _syncState = new SyncState.Idle();
    private string? _selectedMessageId;
    private string? _actionError;
    private MailFolderKind _selectedFolder = MailFolderKind.Inbox;
    private Dictionary<MailFolderKind, List<MailMessage>> _messagesByFolder = [];
    private Dictionary<string, ParsedInboxMeetingRequest> _inboxMeetingRequests = [];
    private readonly Dictionary<string, byte[]> _inlineAttachmentCache = [];
    private readonly HashSet<string> _bodyFetchesInFlight = [];
    private readonly HashSet<string> _knownInboxMessageIds = [];
    private readonly HashSet<string> _notifiedInboxMessageIds = [];
    private bool _inboxNotificationBaselineEstablished;
    private readonly DateTime _serviceStartDate = DateTime.Now;
    private DispatcherTimer? _syncTimer;

    private const int BodyPrefetchLimit = 30;
    private const int ReadBatchSize = 50;
    private const int PreviewLength = 300;

    private readonly CoalescingTaskQueue<MailSyncRequest> _syncQueue;
    private readonly CoalescingTaskQueue<List<MailFolderKind>> _prefetchQueue;

    public List<MailMessage> Messages
    {
        get => _messages;
        private set { SetProperty(ref _messages, value); OnPropertyChanged(nameof(Threads)); OnPropertyChanged(nameof(UnreadCount)); }
    }

    public SyncState SyncState
    {
        get => _syncState;
        private set => SetProperty(ref _syncState, value);
    }

    public string? SelectedMessageId
    {
        get => _selectedMessageId;
        set => SetProperty(ref _selectedMessageId, value);
    }

    public string? ActionError
    {
        get => _actionError;
        set => SetProperty(ref _actionError, value);
    }

    public MailFolderKind SelectedFolder
    {
        get => _selectedFolder;
        private set => SetProperty(ref _selectedFolder, value);
    }

    public List<MailThread> Threads => MailThread.Grouped(Messages);

    public MailThread? Thread(string id, MailFolderKind folder)
    {
        var folderMessages = folder == SelectedFolder ? Messages : (_messagesByFolder.GetValueOrDefault(folder) ?? []);
        return MailThread.Grouped(folderMessages).FirstOrDefault(t => t.Id == id);
    }

    public int UnreadCount => (_messagesByFolder.GetValueOrDefault(MailFolderKind.Inbox) ?? Messages).Count(m => !m.IsRead);

    public MailMessage? SelectedMessage =>
        SelectedMessageId is null ? null : Messages.FirstOrDefault(m => m.Id == SelectedMessageId);

    public List<NormalizedCalendarEvent> InboxInvitations =>
        ActiveSyncParser.NormalizeInboxMeetingRequests(_inboxMeetingRequests.Values.OrderBy(r => r.ServerId));

    private MailSyncService()
    {
        _syncQueue = new CoalescingTaskQueue<MailSyncRequest>(
            (a, b) => a.Merged(b),
            PerformSync);
        _prefetchQueue = new CoalescingTaskQueue<List<MailFolderKind>>(
            (queued, added) => queued.Concat(added.Where(f => !queued.Contains(f))).ToList(),
            PerformBodyPrefetch);

        SettingsStore.Shared.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsStore.IsLoggedIn) or nameof(SettingsStore.MailEnabled))
            {
                if (SettingsStore.Shared.IsLoggedIn && SettingsStore.Shared.MailEnabled)
                {
                    TrayManager.Shared.InstallMail();
                    StartPeriodicSync();
                }
                else DisableMail();
            }
            if (e.PropertyName is nameof(SettingsStore.SyncIntervalMinutes) && SettingsStore.Shared.IsLoggedIn)
                StartPeriodicSync();
        };
        if (SettingsStore.Shared.IsLoggedIn && SettingsStore.Shared.MailEnabled)
        {
            TrayManager.Shared.InstallMail();
            StartPeriodicSync();
        }
    }

    public void StartPeriodicSync()
    {
        StopPeriodicSync();
        _syncTimer = new DispatcherTimer { Interval = SyncInterval };
        _syncTimer.Tick += async (_, _) => await PeriodicSync();
        _syncTimer.Start();
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                RequestSync(MailSyncScope.AllFolders, notifyNew: false));
        });
    }

    public void StopPeriodicSync()
    {
        _syncTimer?.Stop();
        _syncTimer = null;
    }

    public void DisableMail()
    {
        StopPeriodicSync();
        _syncQueue.Cancel();
        _prefetchQueue.Cancel();
        _bodyFetchesInFlight.Clear();
        MailWindowManager.Shared.CloseAll();
        Messages = [];
        _messagesByFolder = [];
        _inboxMeetingRequests = [];
        _inlineAttachmentCache.Clear();
        _knownInboxMessageIds.Clear();
        _notifiedInboxMessageIds.Clear();
        _inboxNotificationBaselineEstablished = false;
        SelectedMessageId = null;
        SelectedFolder = MailFolderKind.Inbox;
        ActionError = null;
        TrayManager.Shared.UpdateMailTitle("");
        TrayManager.Shared.UninstallMail();
    }

    private static TimeSpan SyncInterval
    {
        get
        {
            var minutes = SettingsStore.Shared.SyncIntervalMinutes;
            return minutes <= 0 ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(minutes);
        }
    }

    public Task SyncNow() => RequestSync(MailSyncScope.AllFolders, notifyNew: true);
    public Task SyncInboxForNetworkRecovery() => RequestSync(MailSyncScope.AllFolders, notifyNew: true);

    public void SelectFolder(MailFolderKind folder)
    {
        SelectedFolder = folder;
        SelectedMessageId = null;
        Messages = _messagesByFolder.GetValueOrDefault(folder) ?? [];
        if (folder == MailFolderKind.Drafts) SettingsStore.Shared.DraftsUnsupported = false;
        RequestSync(MailSyncScope.Folder(folder), notifyNew: false);
    }

    public void FocusMessage(string id, MailFolderKind folder = MailFolderKind.Inbox)
    {
        SelectedFolder = folder;
        Messages = _messagesByFolder.GetValueOrDefault(folder) ?? Messages;
        SelectedMessageId = id;
    }

    private Task PeriodicSync() => RequestSync(MailSyncScope.AllFolders, notifyNew: true);

    private Task RequestSync(MailSyncScope scope, bool notifyNew) =>
        _syncQueue.Submit(new MailSyncRequest { Scope = scope, NotifyNew = notifyNew });

    private async Task PerformSync(MailSyncRequest request)
    {
        var folders = request.Scope.Folders(SelectedFolder);
        var isSingleFolder = folders.Count == 1;
        var anyFolderSucceeded = false;
        var draftsFailed = false;
        foreach (var folder in folders)
        {
            if (folder == MailFolderKind.Drafts && SettingsStore.Shared.DraftsUnsupported && folder != SelectedFolder)
                continue;
            var succeeded = await PerformSync(folder, request.NotifyNew && folder == MailFolderKind.Inbox,
                isSingleFolder || folder == SelectedFolder);
            if (succeeded) anyFolderSucceeded = true;
            else if (folder == MailFolderKind.Drafts) draftsFailed = true;
        }
        if (draftsFailed && anyFolderSucceeded) SettingsStore.Shared.DraftsUnsupported = true;
    }

    private async Task<bool> PerformSync(MailFolderKind folder, bool notifyNew, bool updatesUiState)
    {
        var store = SettingsStore.Shared;
        if (!store.IsLoggedIn || store.Password is null) return false;
        if (updatesUiState)
        {
            SyncState = new SyncState.Syncing();
            ActionError = null;
        }
        try
        {
            var forceFullResync = !_messagesByFolder.TryGetValue(folder, out var existing) || existing.Count == 0;
            var snapshot = await new ExchangeClient(store.Account, store.Password)
                .FetchMailMessages(folder, forceFullResync);
            var newUnread = NewUnreadNotifications(snapshot, folder, notifyNew);
            Merge(snapshot, folder);
            if (updatesUiState) SyncState = new SyncState.Success(DateTime.Now);
            RefreshMenuBarTitle();
            if (folder is MailFolderKind.Inbox || folder == SelectedFolder)
                SchedulePrefetchFullBodies(folder);
            foreach (var message in newUnread)
                await DeliverNotification(message);
            return true;
        }
        catch (Exception ex)
        {
            if (updatesUiState)
            {
                SyncState = new SyncState.Failure(ex.Message);
                ActionError = ex.Message;
            }
            return false;
        }
    }

    public Task FetchFullBodyIfNeeded(MailMessage message, MailFolderKind? folder = null) =>
        FetchFullBody(message, folder ?? SelectedFolder);

    private async Task FetchFullBody(MailMessage message, MailFolderKind folder)
    {
        if (!BodyNeedsFetch(message) || _bodyFetchesInFlight.Contains(message.Id)) return;
        _bodyFetchesInFlight.Add(message.Id);
        try
        {
            var accountKey = SettingsStore.Shared.AccountCacheKey;
            var cached = MailBodyCache.Shared.Body(accountKey, message.Id);
            if (cached is { IsTruncated: false } && !string.IsNullOrEmpty(cached.Data))
            {
                ApplyFetchedBody(cached, message);
                return;
            }
            if (SettingsStore.Shared.Password is null) return;
            var body = await new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password)
                .FetchMessageBody(message, folder);
            if (body is null) return;
            var fetchedIsEmpty = string.IsNullOrWhiteSpace(body.Data);
            var existingHasContent = message.Body is { Data.Length: > 0 };
            if (fetchedIsEmpty && existingHasContent) return;
            ApplyFetchedBody(body, message);
            if (!fetchedIsEmpty && SettingsStore.Shared.IsLoggedIn)
                MailBodyCache.Shared.StoreBody(body, accountKey, message.Id);
        }
        finally
        {
            _bodyFetchesInFlight.Remove(message.Id);
        }
    }

    private void ApplyFetchedBody(MailBody body, MailMessage message)
    {
        UpdateMessage(message.Id, m =>
        {
            if (string.IsNullOrEmpty(m.Preview) && m.Body is { Type: MailBodyType.Plain })
                m.Preview = m.Body.Data.Trim().Length > PreviewLength ? m.Body.Data.Trim()[..PreviewLength] : m.Body.Data.Trim();
            m.Body = body;
        });
    }

    private static bool BodyNeedsFetch(MailMessage message)
    {
        if (message.Body is null || message.Body.IsTruncated) return true;
        return SettingsStore.Shared.MailHtmlRenderingEnabled && message.Body.Type != MailBodyType.Html;
    }

    private void SchedulePrefetchFullBodies(MailFolderKind folder)
    {
        if (!SettingsStore.Shared.MailHtmlRenderingEnabled) return;
        _prefetchQueue.Submit([folder]);
    }

    private async Task PerformBodyPrefetch(List<MailFolderKind> folders)
    {
        foreach (var folder in folders)
        {
            var candidateIds = (_messagesByFolder.GetValueOrDefault(folder) ?? [])
                .OrderByDescending(m => m.DateReceived ?? DateTime.MinValue)
                .Take(BodyPrefetchLimit)
                .Where(BodyNeedsFetch)
                .Select(m => m.Id)
                .ToList();
            foreach (var id in candidateIds)
            {
                if (!SettingsStore.Shared.IsLoggedIn) return;
                var current = _messagesByFolder.GetValueOrDefault(folder)?.FirstOrDefault(m => m.Id == id);
                if (current is null) continue;
                await FetchFullBody(current, folder);
                await WarmInlineImages(id, folder);
            }
        }
    }

    private async Task WarmInlineImages(string messageId, MailFolderKind folder)
    {
        if (!SettingsStore.Shared.MailHtmlRenderingEnabled || !SettingsStore.Shared.MailImagesEnabled) return;
        var message = (_messagesByFolder.GetValueOrDefault(folder) ?? []).FirstOrDefault(m => m.Id == messageId);
        if (message?.Body is not { Type: MailBodyType.Html }) return;
        await HtmlWithInlineImages(message.Body.Data, message);
    }

    public async Task<string> HtmlWithInlineImages(string html, MailMessage message)
    {
        if (!MailHtmlInliner.ContainsInlineReferences(html)) return html;
        var payloads = new List<(string ContentId, byte[] Data, string Mime)>();
        foreach (var attachment in message.Attachments)
        {
            if (attachment.ContentId is null) continue;
            var contentId = MailHtmlInliner.NormalizedContentId(attachment.ContentId);
            if (!MailHtmlInliner.References(contentId, html)) continue;
            var data = await InlineAttachmentData(attachment);
            if (data is null) continue;
            var mime = attachment.ContentType ?? MailHtmlInliner.ImageMimeType(data);
            payloads.Add((contentId, data, mime));
        }
        if (payloads.Count == 0) return html;
        var images = payloads.Select(p => new MailInlineImage
        {
            ContentId = p.ContentId,
            MimeType = p.Mime,
            Base64Data = Convert.ToBase64String(p.Data)
        });
        return MailHtmlInliner.ReplacingInlineImages(html, images);
    }

    private async Task<byte[]?> InlineAttachmentData(MailAttachment attachment)
    {
        if (_inlineAttachmentCache.TryGetValue(attachment.FileReference, out var cached)) return cached;
        var accountKey = SettingsStore.Shared.AccountCacheKey;
        var fromDisk = MailBodyCache.Shared.Attachment(accountKey, attachment.FileReference);
        if (fromDisk is not null)
        {
            _inlineAttachmentCache[attachment.FileReference] = fromDisk;
            return fromDisk;
        }
        if (SettingsStore.Shared.Password is null) return null;
        var result = await new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password)
            .FetchAttachment(attachment);
        if (result.Data is null) return null;
        _inlineAttachmentCache[attachment.FileReference] = result.Data;
        if (SettingsStore.Shared.IsLoggedIn)
            MailBodyCache.Shared.StoreAttachment(result.Data, accountKey, attachment.FileReference);
        return result.Data;
    }

    public Task SetRead(MailMessage message, bool read) => PerformAction(async () =>
    {
        if (SettingsStore.Shared.Password is null) return;
        var client = new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password);
        try { await client.SetMessageRead(message, read); }
        catch
        {
            await RequestSync(MailSyncScope.Folder(SelectedFolder), false);
            await client.SetMessageRead(message, read);
        }
        UpdateMessage(message.Id, m => m.IsRead = read);
        RefreshMenuBarTitle();
    });

    public Task MarkAllRead() => PerformAction(async () =>
    {
        var unread = Messages.Where(m => !m.IsRead).ToList();
        if (unread.Count == 0) return;
        if (SettingsStore.Shared.Password is null) return;
        var client = new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password);
        var didResync = false;
        foreach (var batch in unread.Chunk(ReadBatchSize))
        {
            try { await client.SetMessagesRead(batch, true); }
            catch when (!didResync)
            {
                didResync = true;
                await RequestSync(MailSyncScope.Folder(SelectedFolder), false);
                await client.SetMessagesRead(batch, true);
            }
            foreach (var message in batch)
                UpdateMessage(message.Id, m => m.IsRead = true);
            RefreshMenuBarTitle();
        }
    });

    public Task Reply(MailMessage message, string body, bool replyAll) => PerformAction(async () =>
    {
        if (SettingsStore.Shared.Password is null) return;
        await new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password).Reply(message, body, replyAll);
        await SyncNow();
    });

    public Task Forward(MailMessage message, string rawRecipients, string body) => PerformAction(async () =>
    {
        if (SettingsStore.Shared.Password is null) return;
        await new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password)
            .Forward(message, ParseRecipients(rawRecipients), body);
        await SyncNow();
    });

    public Task Send(string rawTo, string rawCc, string subject, string body) => PerformAction(async () =>
    {
        if (SettingsStore.Shared.Password is null) return;
        await new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password)
            .SendMail(ParseRecipients(rawTo), ParseRecipients(rawCc), subject, body);
        await SyncNow();
    });

    public Task Download(MailAttachment attachment) => PerformAction(async () =>
    {
        if (SettingsStore.Shared.Password is null) return;
        var result = await new ExchangeClient(SettingsStore.Shared.Account, SettingsStore.Shared.Password).FetchAttachment(attachment);
        if (result.Data is null) throw ExchangeException.ActiveSync("Вложение не содержит данных.");
        var fileName = !string.IsNullOrEmpty(result.FileName) ? result.FileName! : attachment.DisplayName;
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var path = Path.Combine(downloads, fileName);
        await File.WriteAllBytesAsync(path, result.Data);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    });

    public void RefreshMenuBarTitle()
    {
        TrayManager.Shared.UpdateMailTitle(UnreadCount > 0 ? $"{UnreadCount}" : "");
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(Threads));
    }

    private void Merge(MailSyncSnapshot snapshot, MailFolderKind folder)
    {
        var byId = (_messagesByFolder.GetValueOrDefault(folder) ?? []).ToDictionary(m => m.Id);
        foreach (var deleted in snapshot.DeletedServerIds) byId.Remove(deleted);
        foreach (var message in snapshot.Messages)
        {
            var incoming = message;
            if (byId.TryGetValue(message.Id, out var existing)
                && existing.Body is { Type: MailBodyType.Html, IsTruncated: false, Data.Length: > 0 }
                && incoming.Body is not { Type: MailBodyType.Html, IsTruncated: false })
            {
                incoming.Body = existing.Body;
            }
            byId[incoming.Id] = incoming;
        }
        var folderMessages = byId.Values.OrderByDescending(m => m.DateReceived ?? DateTime.MinValue).ToList();
        _messagesByFolder[folder] = folderMessages;
        if (SelectedFolder == folder) Messages = folderMessages;
        if (folder == MailFolderKind.Inbox)
        {
            foreach (var id in folderMessages.Select(m => m.Id)) _knownInboxMessageIds.Add(id);
            _inboxNotificationBaselineEstablished = true;
            MergeInboxMeetingRequests(snapshot);
        }
    }

    private void MergeInboxMeetingRequests(MailSyncSnapshot snapshot)
    {
        var updated = new Dictionary<string, ParsedInboxMeetingRequest>(_inboxMeetingRequests);
        foreach (var deleted in snapshot.DeletedServerIds) updated.Remove(deleted);
        foreach (var request in snapshot.MeetingRequests.Where(r => !string.IsNullOrEmpty(r.ServerId)))
            updated[request.ServerId] = request;
        if (updated.Count == _inboxMeetingRequests.Count && updated.Keys.All(_inboxMeetingRequests.ContainsKey)) return;
        _inboxMeetingRequests = updated;
        _ = CalendarSyncService.Shared.SyncNow();
    }

    private List<MailMessage> NewUnreadNotifications(MailSyncSnapshot snapshot, MailFolderKind folder, bool notifyNew)
    {
        if (!notifyNew || folder != MailFolderKind.Inbox) return [];
        var previous = (_messagesByFolder.GetValueOrDefault(MailFolderKind.Inbox) ?? []).ToDictionary(m => m.Id);
        var existingIds = _knownInboxMessageIds.Union(previous.Keys).ToHashSet();
        return snapshot.Messages.Where(message =>
        {
            if (message.IsRead || _notifiedInboxMessageIds.Contains(message.Id)) return false;
            if (_inboxNotificationBaselineEstablished)
            {
                var isNew = !existingIds.Contains(message.Id);
                var becameUnread = previous.TryGetValue(message.Id, out var prev) && prev.IsRead;
                return isNew || becameUnread;
            }
            return message.DateReceived is { } received && received >= _serviceStartDate;
        }).ToList();
    }

    private async Task DeliverNotification(MailMessage message)
    {
        try
        {
            await NotificationService.Shared.DeliverNewMailNotification(message);
            _notifiedInboxMessageIds.Add(message.Id);
        }
        catch (Exception ex)
        {
            ActionError = ex.Message;
        }
    }

    private void UpdateMessage(string id, Action<MailMessage> mutate)
    {
        var didUpdateSelected = false;
        foreach (var folder in _messagesByFolder.Keys.ToList())
        {
            var folderMessages = _messagesByFolder[folder];
            var index = folderMessages.FindIndex(m => m.Id == id);
            if (index < 0) continue;
            mutate(folderMessages[index]);
            _messagesByFolder[folder] = folderMessages;
            if (folder == SelectedFolder)
            {
                Messages = folderMessages.ToList();
                didUpdateSelected = true;
            }
        }
        if (didUpdateSelected) return;
        var i = Messages.FindIndex(m => m.Id == id);
        if (i >= 0)
        {
            mutate(Messages[i]);
            Messages = Messages.ToList();
        }
    }

    private async Task PerformAction(Func<Task> action)
    {
        ActionError = null;
        try { await action(); }
        catch (Exception ex) { ActionError = ex.Message; }
    }

    private static List<MailAddress> ParseRecipients(string raw) =>
        raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)
            .Select(s => new MailAddress { Name = "", Email = s }).ToList();
}
