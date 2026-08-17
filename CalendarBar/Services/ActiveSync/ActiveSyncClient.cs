using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace CalendarBar;

public sealed class ExchangeException : Exception
{
    public ExchangeException(string message) : base(message) { }

    public static ExchangeException InvalidResponse() => new("Некорректный ответ сервера");
    public static ExchangeException Unauthorized() => new("Неверный логин или пароль");
    public static ExchangeException ServerError(int code) => new($"Ошибка сервера ({code})");
    public static ExchangeException ParseError() => new("Не удалось разобрать ответ календаря");
    public static ExchangeException ActiveSync(string message) => new(message);
    public static ExchangeException FolderResyncRequired() => new("Структура папок изменилась");

    public bool IsFolderResync => Message == "Структура папок изменилась";
}

public sealed class ActiveSyncClient
{
    public const string DefaultUserAgent = "Apple-iPhone14C3/1704.10";
    public const string DefaultProtocolVersion = "14.1";
    public const string DefaultDeviceType = "iPhone";

    private AccountSettings _settings;
    private readonly string _password;
    private string _endpoint;
    private string _policyKey = "0";
    private readonly HttpClient _http;

    private static readonly Dictionary<string, List<string>> ProtocolVersionsCache = [];
    private static readonly object ProtocolVersionsLock = new();

    public ActiveSyncClient(AccountSettings settings, string password)
    {
        _settings = settings;
        _password = password;
        _endpoint = settings.ActiveSyncEndpoint;
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
    }

    public string CurrentDeviceId => _settings.ResolvedDeviceId;
    private string AccountStorageKey => _settings.Email.Trim().ToLowerInvariant();

    public Task TestConnection() => GetCalendarEvents(maxPages: 1, windowSize: 10, inboxInvitations: []);

    public async Task<List<NormalizedCalendarEvent>> GetCalendarEvents(
        int maxPages = 10, int windowSize = 50,
        List<NormalizedCalendarEvent>? inboxInvitations = null,
        int reprovisionAttempts = 0, bool deviceIdRetry = false, int folderResyncAttempts = 0)
    {
        await EnsureEndpoint();
        if (_policyKey == "0")
        {
            try { await PerformProvisioning(); } catch { }
        }

        var folders = await LoadFolders(reprovisionAttempts, deviceIdRetry);
        var calendarFolder = ActiveSyncParser.FindCalendarFolder(folders)
            ?? throw ExchangeException.ActiveSync("Calendar folder not found in FolderSync response.");

        try
        {
            var events = await SyncCalendarFolder(calendarFolder, maxPages, windowSize);
            if (inboxInvitations is not null)
                events = ActiveSyncParser.MergeCalendarEventsWithInvitations(events, inboxInvitations);
            else
            {
                var inboxFolder = ActiveSyncParser.FindInboxFolder(folders);
                if (inboxFolder is not null)
                {
                    var invitations = await SyncInboxMeetingRequests(inboxFolder, maxPages, 100);
                    events = ActiveSyncParser.MergeCalendarEventsWithInvitations(events, invitations);
                }
            }
            return events;
        }
        catch (ExchangeException ex) when (ex.IsFolderResync)
        {
            if (folderResyncAttempts >= 1)
                throw ExchangeException.ActiveSync("Calendar Sync failed: folder resync exceeded retry limit.");
            ActiveSyncSyncKeyStore.Shared.UpdateCalendar("0", AccountStorageKey);
            return await GetCalendarEvents(maxPages, windowSize, inboxInvitations, reprovisionAttempts, deviceIdRetry, folderResyncAttempts + 1);
        }
    }

    public Task<MailSyncSnapshot> GetInboxMessages(int maxPages = 20, int windowSize = 100, int folderResyncAttempts = 0, bool forceFullResync = false) =>
        GetMailMessages(MailFolderKind.Inbox, maxPages, windowSize, folderResyncAttempts, forceFullResync);

    public async Task<MailSyncSnapshot> GetMailMessages(MailFolderKind kind, int maxPages = 20, int windowSize = 100, int folderResyncAttempts = 0, bool forceFullResync = false)
    {
        await EnsureEndpoint();
        if (_policyKey == "0")
        {
            try { await PerformProvisioning(); } catch { }
        }
        var protocolVersion = await MailProtocolVersion(kind);
        var folders = await LoadFolders();
        var folder = ActiveSyncParser.FindMailFolder(folders, kind)
            ?? throw ExchangeException.ActiveSync($"Mail folder {kind.Title()} not found in FolderSync response.");
        try
        {
            return await SyncMailMessages(folder, maxPages, windowSize, protocolVersion, kind != MailFolderKind.Drafts, forceFullResync);
        }
        catch (ExchangeException ex) when (ex.IsFolderResync)
        {
            if (folderResyncAttempts >= 1)
                throw ExchangeException.ActiveSync($"{kind.Title()} Sync failed: folder resync exceeded retry limit.");
            ActiveSyncSyncKeyStore.Shared.UpdateMailFolder("0", AccountStorageKey, folder.ServerId);
            if (kind == MailFolderKind.Inbox)
                ActiveSyncSyncKeyStore.Shared.UpdateInbox("0", AccountStorageKey);
            return await GetMailMessages(kind, maxPages, windowSize, folderResyncAttempts + 1, true);
        }
    }

    public async Task<string> MailProtocolVersion(MailFolderKind kind)
    {
        if (kind != MailFolderKind.Drafts) return DefaultProtocolVersion;
        var supported = await SupportedProtocolVersions();
        if (supported.Contains("16.1")) return "16.1";
        if (supported.Contains("16.0")) return "16.0";
        throw ExchangeException.ActiveSync(
            $"Сервер не поддерживает синхронизацию черновиков (требуется ActiveSync 16.0, доступно: {string.Join(", ", supported)}).");
    }

    private async Task<List<string>> SupportedProtocolVersions()
    {
        try { await EnsureEndpoint(); } catch { return []; }
        lock (ProtocolVersionsLock)
        {
            if (ProtocolVersionsCache.TryGetValue(_endpoint, out var cached)) return cached;
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, _endpoint);
            request.Headers.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
            request.Headers.TryAddWithoutValidation("Authorization", BasicAuthHeader());
            using var response = await _http.SendAsync(request);
            if (!response.Headers.TryGetValues("MS-ASProtocolVersions", out var values)) return [];
            var versions = values.SelectMany(v => v.Split(',')).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
            if (versions.Count == 0) return [];
            lock (ProtocolVersionsLock) ProtocolVersionsCache[_endpoint] = versions;
            return versions;
        }
        catch { return []; }
    }

    public async Task<MailBody?> FetchMessageBody(string collectionId, string serverId, MailBodyType preferredBodyType = MailBodyType.Html, string protocolVersion = DefaultProtocolVersion, int reprovisionAttempts = 0)
    {
        var xml = await ExecuteCommand("ItemOperations",
            ActiveSyncParser.BuildItemOperationsFetchMessageXml(collectionId, serverId, preferredBodyType),
            protocolVersion: protocolVersion);
        var result = ActiveSyncParser.ParseItemOperationsFetchXml(xml);
        if (CommandNeedsReprovision(result.Status) && reprovisionAttempts < 1)
        {
            await PerformProvisioning();
            return await FetchMessageBody(collectionId, serverId, preferredBodyType, protocolVersion, reprovisionAttempts + 1);
        }
        if (!string.IsNullOrEmpty(result.Status) && result.Status != "1")
            throw ExchangeException.ActiveSync($"ItemOperations Fetch failed with ActiveSync status {result.Status}.");
        return result.Body;
    }

    public async Task<ItemOperationsFetchResult> FetchAttachment(string fileReference, int reprovisionAttempts = 0)
    {
        var xml = await ExecuteCommand("ItemOperations", ActiveSyncParser.BuildItemOperationsFetchAttachmentXml(fileReference));
        var result = ActiveSyncParser.ParseItemOperationsFetchXml(xml);
        if (CommandNeedsReprovision(result.Status) && reprovisionAttempts < 1)
        {
            await PerformProvisioning();
            return await FetchAttachment(fileReference, reprovisionAttempts + 1);
        }
        if (!string.IsNullOrEmpty(result.Status) && result.Status != "1")
            throw ExchangeException.ActiveSync($"Attachment fetch failed with ActiveSync status {result.Status}.");
        return result;
    }

    public Task SetMessageRead(string collectionId, string serverId, bool read) =>
        SetMessagesRead(collectionId, [serverId], read);

    public async Task SetMessagesRead(string collectionId, List<string> serverIds, bool read, int reprovisionAttempts = 0, int resyncAttempts = 0)
    {
        if (serverIds.Count == 0) return;
        var syncKey = ActiveSyncSyncKeyStore.Shared.MailFolderSyncKey(AccountStorageKey, collectionId);
        if (syncKey == "0") syncKey = await RefreshMailFolderSyncKey(collectionId);
        var xml = await ExecuteCommand("Sync", ActiveSyncParser.BuildReadChangeRequestXml(syncKey, collectionId, serverIds, read));
        var status = ActiveSyncParser.ParseSyncCommandStatusXml(xml);
        var effectiveStatuses = serverIds.Select(id =>
        {
            if (status.ItemStatuses.TryGetValue(id, out var item) && !string.IsNullOrEmpty(item)) return item;
            return status.Status;
        }).ToList();
        if (effectiveStatuses.Any(CommandNeedsReprovision) && reprovisionAttempts < 1)
        {
            await PerformProvisioning();
            await SetMessagesRead(collectionId, serverIds, read, reprovisionAttempts + 1, resyncAttempts);
            return;
        }
        if (ActiveSyncSyncStatus.RequiresFullResync(status.Status) && resyncAttempts < 1)
        {
            ActiveSyncSyncKeyStore.Shared.UpdateMailFolder("0", AccountStorageKey, collectionId);
            await SetMessagesRead(collectionId, serverIds, read, reprovisionAttempts, resyncAttempts + 1);
            return;
        }
        if (effectiveStatuses.Any(s => ActiveSyncSyncStatus.RequiresFullResync(s) || ActiveSyncSyncStatus.RequiresFolderResync(s)))
        {
            ActiveSyncSyncKeyStore.Shared.UpdateMailFolder("0", AccountStorageKey, collectionId);
            throw ExchangeException.FolderResyncRequired();
        }
        var failed = effectiveStatuses.FirstOrDefault(s => !string.IsNullOrEmpty(s) && s != "1");
        if (failed is not null)
            throw ExchangeException.ActiveSync($"Read state update failed with ActiveSync status {failed}.");
        if (!string.IsNullOrEmpty(status.SyncKey))
            ActiveSyncSyncKeyStore.Shared.UpdateMailFolder(status.SyncKey, AccountStorageKey, collectionId);
    }

    private async Task<string> RefreshMailFolderSyncKey(string collectionId)
    {
        var folders = await LoadFolders();
        var folder = folders.FirstOrDefault(f => f.ServerId == collectionId)
            ?? throw ExchangeException.ActiveSync($"Mail folder for collection {collectionId} was not found.");
        await SyncMailMessages(folder, 5, 50);
        var syncKey = ActiveSyncSyncKeyStore.Shared.MailFolderSyncKey(AccountStorageKey, collectionId);
        if (syncKey == "0") throw ExchangeException.ActiveSync("Mail folder sync key is not ready yet. Refresh mail first.");
        return syncKey;
    }

    public async Task SendMail(List<MailAddress> to, List<MailAddress> cc, string subject, string body)
    {
        var mime = BuildMimeMessage(to, cc, subject, body);
        var xml = await ExecuteCommand("SendMail", ActiveSyncParser.BuildSendMailRequestXml(Guid.NewGuid().ToString(), mime));
        var status = ActiveSyncParser.ParseSimpleCommandStatusXml(xml);
        if (!string.IsNullOrEmpty(status.Status) && status.Status != "1")
            throw ExchangeException.ActiveSync($"SendMail failed with ActiveSync status {status.Status}.");
    }

    public async Task SmartReply(MailMessage message, string body, bool replyAll)
    {
        var recipients = ReplyRecipients(message, replyAll);
        var mime = BuildMimeMessage(recipients.To, recipients.Cc, ReplySubject(message.Subject), body);
        var xml = await ExecuteCommand("SmartReply", ActiveSyncParser.BuildSmartReplyRequestXml(message.CollectionId, message.ServerId, mime));
        var status = ActiveSyncParser.ParseSimpleCommandStatusXml(xml);
        if (!string.IsNullOrEmpty(status.Status) && status.Status != "1")
            throw ExchangeException.ActiveSync($"SmartReply failed with ActiveSync status {status.Status}.");
    }

    public async Task SmartForward(MailMessage message, List<MailAddress> to, string body)
    {
        var mime = BuildMimeMessage(to, [], ForwardSubject(message.Subject), body);
        var xml = await ExecuteCommand("SmartForward", ActiveSyncParser.BuildSmartForwardRequestXml(message.CollectionId, message.ServerId, mime));
        var status = ActiveSyncParser.ParseSimpleCommandStatusXml(xml);
        if (!string.IsNullOrEmpty(status.Status) && status.Status != "1")
            throw ExchangeException.ActiveSync($"SmartForward failed with ActiveSync status {status.Status}.");
    }

    public async Task RespondToMeeting(string requestId, string collectionId, MeetingAction action)
    {
        var xml = await ExecuteCommand("MeetingResponse", ActiveSyncParser.BuildMeetingResponseRequestXml(requestId, collectionId, action));
        var status = ActiveSyncParser.ParseSimpleCommandStatusXml(xml);
        if (!string.IsNullOrEmpty(status.Status) && status.Status != "1")
            throw ExchangeException.ActiveSync($"MeetingResponse failed with ActiveSync status {status.Status}.");
    }

    public async Task RespondToMeeting(string serverId, MeetingAction action)
    {
        var folders = await LoadFolders();
        var isInboxInvitation = serverId.StartsWith("inbox:");
        var requestId = isInboxInvitation ? serverId["inbox:".Length..] : serverId;
        var folder = isInboxInvitation ? ActiveSyncParser.FindInboxFolder(folders) : ActiveSyncParser.FindCalendarFolder(folders);
        if (folder is null) throw ExchangeException.ActiveSync("Exchange folder for meeting response was not found.");
        await RespondToMeeting(requestId, folder.ServerId, action);
    }

    public async Task DeleteCalendarEvent(string collectionId, string serverId)
    {
        var keys = ActiveSyncSyncKeyStore.Shared.Load(AccountStorageKey);
        if (keys.Calendar == "0") throw ExchangeException.ActiveSync("Calendar sync key is not ready yet. Refresh calendar first.");
        var xml = await ExecuteCommand("Sync", ActiveSyncParser.BuildCalendarDeleteRequestXml(keys.Calendar, collectionId, serverId));
        var status = ActiveSyncParser.ParseSyncCommandStatusXml(xml);
        if (!string.IsNullOrEmpty(status.Status) && status.Status != "1")
            throw ExchangeException.ActiveSync($"Calendar delete failed with ActiveSync status {status.Status}.");
        if (!string.IsNullOrEmpty(status.SyncKey))
            ActiveSyncSyncKeyStore.Shared.UpdateCalendar(status.SyncKey, AccountStorageKey);
    }

    public async Task DeleteCalendarEvent(string serverId)
    {
        var rawServerId = serverId.StartsWith("inbox:") ? serverId["inbox:".Length..] : serverId;
        var folders = await LoadFolders();
        var calendarFolder = ActiveSyncParser.FindCalendarFolder(folders)
            ?? throw ExchangeException.ActiveSync("Calendar folder was not found.");
        await DeleteCalendarEvent(calendarFolder.ServerId, rawServerId);
    }

    private async Task<List<FolderRecord>> LoadFolders(int reprovisionAttempts = 0, bool deviceIdRetry = false, bool transientRetry = false)
    {
        await EnsureEndpoint();
        if (_policyKey == "0")
        {
            try { await PerformProvisioning(); } catch { }
        }
        var xml = await ExecuteCommand("FolderSync", ActiveSyncParser.BuildFolderSyncRequestXml("0"));
        var folderSync = ActiveSyncParser.ParseFolderSyncXml(xml);

        if (folderSync.Status == "108" && !deviceIdRetry)
        {
            _settings.DeviceId = AccountSettings.GenerateDeviceId();
            return await LoadFolders(reprovisionAttempts, true, transientRetry);
        }
        if (folderSync.Status is "142" or "144" or "6")
        {
            if (folderSync.Status == "6" && !transientRetry)
            {
                await Task.Delay(500);
                return await LoadFolders(reprovisionAttempts, deviceIdRetry, true);
            }
            if (reprovisionAttempts >= 1)
                throw ExchangeException.ActiveSync($"FolderSync failed with ActiveSync status {folderSync.Status} after reprovision retry.");
            await PerformProvisioning();
            return await LoadFolders(reprovisionAttempts + 1, deviceIdRetry, transientRetry);
        }
        if (folderSync.Status != "1")
            throw ExchangeException.ActiveSync($"FolderSync failed with ActiveSync status {folderSync.Status}. {ActiveSyncStatusHint(folderSync.Status)}");
        return folderSync.Folders;
    }

    private enum SyncFailureKind { FullResync, FolderResync, Fatal }

    private sealed class CalendarSyncPageResult
    {
        public string SyncKey { get; init; } = "";
        public List<ParsedCalendarEvent> Events { get; init; } = [];
        public bool MoreAvailable { get; init; }
        public SyncFailureKind? Failure { get; init; }
        public string? FatalStatus { get; init; }
    }

    private async Task<List<NormalizedCalendarEvent>> SyncCalendarFolder(FolderRecord calendarFolder, int maxPages, int windowSize)
    {
        var fullResyncAttempts = 0;
        const int maxFullResyncs = 2;
        while (fullResyncAttempts <= maxFullResyncs)
        {
            var events = new List<NormalizedCalendarEvent>();
            var syncKey = "0";
            var needsFullResync = false;
            for (var page = 0; page < maxPages; page++)
            {
                var requestSyncKey = syncKey;
                var parsed = await PerformCalendarSyncPage(syncKey, calendarFolder.ServerId, windowSize);
                if (parsed.Failure is SyncFailureKind.FullResync) { needsFullResync = true; break; }
                if (parsed.Failure is SyncFailureKind.FolderResync) throw ExchangeException.FolderResyncRequired();
                if (parsed.Failure is SyncFailureKind.Fatal)
                    throw ExchangeException.ActiveSync($"Calendar Sync failed with ActiveSync status {parsed.FatalStatus}. {ActiveSyncSyncStatus.UserMessage(parsed.FatalStatus ?? "")}");
                syncKey = string.IsNullOrEmpty(parsed.SyncKey) ? syncKey : parsed.SyncKey;
                ActiveSyncSyncKeyStore.Shared.UpdateCalendar(syncKey, AccountStorageKey);
                events.AddRange(ActiveSyncParser.NormalizeCalendarEvents(parsed.Events));
                if (requestSyncKey == "0" && parsed.Events.Count == 0 && syncKey != "0") continue;
                if (!parsed.MoreAvailable) break;
            }
            if (needsFullResync) { fullResyncAttempts++; continue; }
            return ActiveSyncParser.SortCalendarEventsByStart(events);
        }
        throw ExchangeException.ActiveSync("Calendar Sync failed after resync retries.");
    }

    private async Task<CalendarSyncPageResult> PerformCalendarSyncPage(string syncKey, string collectionId, int windowSize)
    {
        for (var transientAttempt = 0; transientAttempt < 2; transientAttempt++)
        {
            var xml = await ExecuteCommand("Sync", ActiveSyncParser.BuildCalendarSyncRequestXml(DefaultProtocolVersion, syncKey, collectionId, windowSize));
            var parsed = ActiveSyncParser.ParseCalendarSyncXml(xml);
            var status = parsed.Status;
            if (string.IsNullOrEmpty(status) || status == "1")
                return new CalendarSyncPageResult { SyncKey = parsed.SyncKey, Events = parsed.Events, MoreAvailable = parsed.MoreAvailable };
            if (ActiveSyncSyncStatus.RequiresFullResync(status))
                return new CalendarSyncPageResult { Failure = SyncFailureKind.FullResync };
            if (ActiveSyncSyncStatus.RequiresFolderResync(status))
                return new CalendarSyncPageResult { Failure = SyncFailureKind.FolderResync };
            if (ActiveSyncSyncStatus.IsTransient(status) && transientAttempt == 0)
            {
                await Task.Delay(500);
                continue;
            }
            return new CalendarSyncPageResult { Failure = SyncFailureKind.Fatal, FatalStatus = status };
        }
        throw ExchangeException.ActiveSync("Calendar Sync failed after transient retries.");
    }

    private sealed class InboxSyncPageResult
    {
        public string SyncKey { get; init; } = "";
        public List<ParsedInboxMeetingRequest> MeetingRequests { get; init; } = [];
        public List<MailMessage> Messages { get; init; } = [];
        public List<string> DeletedServerIds { get; init; } = [];
        public bool MoreAvailable { get; init; }
        public SyncFailureKind? Failure { get; init; }
        public string? FatalStatus { get; init; }
    }

    private async Task<InboxSyncPageResult> PerformInboxSyncPage(string syncKey, string collectionId, int windowSize, string protocolVersion = DefaultProtocolVersion, bool includeFilterType = true)
    {
        for (var transientAttempt = 0; transientAttempt < 2; transientAttempt++)
        {
            var xml = await ExecuteCommand("Sync",
                ActiveSyncParser.BuildInboxSyncRequestXml(protocolVersion, syncKey, collectionId, windowSize, includeFilterType),
                protocolVersion: protocolVersion);
            var parsed = ActiveSyncParser.ParseInboxSyncXml(xml, collectionId);
            var status = parsed.Status;
            if (string.IsNullOrEmpty(status) || status == "1")
                return new InboxSyncPageResult
                {
                    SyncKey = parsed.SyncKey,
                    MeetingRequests = parsed.MeetingRequests,
                    Messages = parsed.Messages,
                    DeletedServerIds = parsed.DeletedServerIds,
                    MoreAvailable = parsed.MoreAvailable
                };
            if (ActiveSyncSyncStatus.RequiresFullResync(status))
                return new InboxSyncPageResult { Failure = SyncFailureKind.FullResync };
            if (ActiveSyncSyncStatus.RequiresFolderResync(status))
                return new InboxSyncPageResult { Failure = SyncFailureKind.FolderResync };
            if (ActiveSyncSyncStatus.IsTransient(status) && transientAttempt == 0)
            {
                await Task.Delay(500);
                continue;
            }
            return new InboxSyncPageResult { Failure = SyncFailureKind.Fatal, FatalStatus = status };
        }
        throw ExchangeException.ActiveSync("Inbox Sync failed after transient retries.");
    }

    private async Task<List<NormalizedCalendarEvent>> SyncInboxMeetingRequests(FolderRecord inboxFolder, int maxPages, int windowSize)
    {
        var fullResyncAttempts = 0;
        const int maxFullResyncs = 2;
        while (fullResyncAttempts <= maxFullResyncs)
        {
            var requests = new List<ParsedInboxMeetingRequest>();
            var syncKey = "0";
            var needsFullResync = false;
            for (var page = 0; page < maxPages; page++)
            {
                var requestSyncKey = syncKey;
                var parsed = await PerformInboxSyncPage(syncKey, inboxFolder.ServerId, windowSize);
                if (parsed.Failure is SyncFailureKind.FullResync) { needsFullResync = true; break; }
                if (parsed.Failure is SyncFailureKind.FolderResync) throw ExchangeException.FolderResyncRequired();
                if (parsed.Failure is SyncFailureKind.Fatal)
                    throw ExchangeException.ActiveSync($"Inbox Sync failed with ActiveSync status {parsed.FatalStatus}. {ActiveSyncSyncStatus.UserMessage(parsed.FatalStatus ?? "")}");
                syncKey = string.IsNullOrEmpty(parsed.SyncKey) ? syncKey : parsed.SyncKey;
                requests.AddRange(parsed.MeetingRequests);
                if (requestSyncKey == "0" && parsed.MeetingRequests.Count == 0 && syncKey != "0") continue;
                if (!parsed.MoreAvailable) break;
            }
            if (needsFullResync) { fullResyncAttempts++; continue; }
            return ActiveSyncParser.NormalizeInboxMeetingRequests(requests);
        }
        throw ExchangeException.ActiveSync("Inbox Sync failed after resync retries.");
    }

    private async Task<MailSyncSnapshot> SyncMailMessages(FolderRecord mailFolder, int maxPages, int windowSize, string protocolVersion = DefaultProtocolVersion, bool includeFilterType = true, bool forceFullResync = false)
    {
        var fullResyncAttempts = 0;
        const int maxFullResyncs = 2;
        while (fullResyncAttempts <= maxFullResyncs)
        {
            var messages = new List<MailMessage>();
            var deletedServerIds = new List<string>();
            var meetingRequests = new List<ParsedInboxMeetingRequest>();
            var syncKey = forceFullResync ? "0" : ActiveSyncSyncKeyStore.Shared.MailFolderSyncKey(AccountStorageKey, mailFolder.ServerId);
            var needsFullResync = false;
            for (var i = 0; i < maxPages; i++)
            {
                var requestSyncKey = syncKey;
                var parsed = await PerformInboxSyncPage(syncKey, mailFolder.ServerId, windowSize, protocolVersion, includeFilterType);
                if (parsed.Failure is SyncFailureKind.FullResync)
                {
                    ActiveSyncSyncKeyStore.Shared.UpdateMailFolder("0", AccountStorageKey, mailFolder.ServerId);
                    syncKey = "0";
                    needsFullResync = true;
                    break;
                }
                if (parsed.Failure is SyncFailureKind.FolderResync) throw ExchangeException.FolderResyncRequired();
                if (parsed.Failure is SyncFailureKind.Fatal)
                    throw ExchangeException.ActiveSync($"Inbox Sync failed with ActiveSync status {parsed.FatalStatus}. {ActiveSyncSyncStatus.UserMessage(parsed.FatalStatus ?? "")}");
                syncKey = string.IsNullOrEmpty(parsed.SyncKey) ? syncKey : parsed.SyncKey;
                ActiveSyncSyncKeyStore.Shared.UpdateMailFolder(syncKey, AccountStorageKey, mailFolder.ServerId);
                if (ActiveSyncParser.FindInboxFolder([mailFolder]) is not null)
                    ActiveSyncSyncKeyStore.Shared.UpdateInbox(syncKey, AccountStorageKey);
                messages.AddRange(parsed.Messages);
                deletedServerIds.AddRange(parsed.DeletedServerIds);
                meetingRequests.AddRange(parsed.MeetingRequests);
                if (requestSyncKey == "0" && parsed.Messages.Count == 0 && parsed.DeletedServerIds.Count == 0 && syncKey != "0") continue;
                if (!parsed.MoreAvailable) break;
            }
            if (needsFullResync) { fullResyncAttempts++; continue; }
            return new MailSyncSnapshot
            {
                Messages = messages.OrderByDescending(m => m.DateReceived ?? DateTime.MinValue).ToList(),
                DeletedServerIds = deletedServerIds,
                MeetingRequests = meetingRequests
            };
        }
        throw ExchangeException.ActiveSync("Inbox Sync failed after resync retries.");
    }

    private string BuildMimeMessage(List<MailAddress> to, List<MailAddress> cc, string subject, string body)
    {
        var lines = new List<string>
        {
            $"To: {FormatAddresses(to)}",
            $"Subject: {EncodedHeader(subject)}",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: 8bit"
        };
        if (cc.Count > 0) lines.Insert(1, $"Cc: {FormatAddresses(cc)}");
        lines.Add("");
        lines.Add(body);
        return string.Join("\r\n", lines);
    }

    private static string FormatAddresses(IEnumerable<MailAddress> addresses) =>
        string.Join(", ", addresses.Where(a => !string.IsNullOrEmpty(a.Email) || !string.IsNullOrEmpty(a.Name))
            .Select(a => string.IsNullOrEmpty(a.Email) ? a.Name
                : string.IsNullOrEmpty(a.Name) ? a.Email
                : $"\"{a.Name.Replace("\"", "")}\" <{a.Email}>"));

    private static string EncodedHeader(string value)
    {
        try
        {
            if (value.All(c => c <= 127)) return value;
        }
        catch { }
        return $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";
    }

    private (List<MailAddress> To, List<MailAddress> Cc) ReplyRecipients(MailMessage message, bool replyAll)
    {
        var selfEmail = _settings.Email.Trim().ToLowerInvariant();
        var primary = message.ReplyTo.Count > 0 ? message.ReplyTo : message.From is null ? [] : [message.From];
        if (!replyAll) return (primary.Where(a => a.Email.ToLowerInvariant() != selfEmail).ToList(), []);
        var to = primary.Concat(message.To).Where(a => a.Email.ToLowerInvariant() != selfEmail).ToList();
        var cc = message.Cc.Where(a => a.Email.ToLowerInvariant() != selfEmail).ToList();
        return (DedupeAddresses(to), DedupeAddresses(cc));
    }

    private static List<MailAddress> DedupeAddresses(List<MailAddress> addresses)
    {
        var seen = new HashSet<string>();
        return addresses.Where(a => seen.Add(string.IsNullOrEmpty(a.Email) ? a.Name.ToLowerInvariant() : a.Email.ToLowerInvariant())).ToList();
    }

    private static bool CommandNeedsReprovision(string status) => status is "142" or "144";
    private static string ReplySubject(string subject) => subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase) ? subject : $"Re: {subject}";
    private static string ForwardSubject(string subject) =>
        subject.StartsWith("fw:", StringComparison.OrdinalIgnoreCase) || subject.StartsWith("fwd:", StringComparison.OrdinalIgnoreCase) ? subject : $"Fw: {subject}";

    public async Task PerformProvisioning()
    {
        var config = new ProvisionRequestConfig
        {
            DeviceModel = DefaultDeviceType,
            DeviceImei = "000000000000000",
            DeviceFriendlyName = "CalendarBar iPhone",
            DeviceOs = "iOS 18.0",
            DeviceOsLanguage = "en-us",
            DevicePhoneNumber = "0000000000",
            DeviceMobileOperator = "Unknown",
            UserAgent = DefaultUserAgent
        };
        var initialXml = await ExecuteCommand("Provision", ActiveSyncParser.BuildInitialProvisionRequestXml(config), policyKeyOverride: "0");
        var initial = ActiveSyncParser.ParseProvisionResponseXml(initialXml);
        if (initial.Status != "1" || string.IsNullOrEmpty(initial.PolicyKey))
            throw ExchangeException.ActiveSync("Provision phase 1 failed.");
        var ackXml = await ExecuteCommand("Provision", ActiveSyncParser.BuildProvisionAckRequestXml(initial.PolicyKey, "1"), policyKeyOverride: initial.PolicyKey);
        var ack = ActiveSyncParser.ParseProvisionResponseXml(ackXml);
        if (ack.Status != "1" || ack.PolicyStatus != "1" || string.IsNullOrEmpty(ack.PolicyKey))
            throw ExchangeException.ActiveSync("Provision phase 2 failed.");
        _policyKey = ack.PolicyKey;
    }

    private async Task EnsureEndpoint()
    {
        if (!string.IsNullOrEmpty(_endpoint)) return;
        _endpoint = await DiscoverEndpoint();
    }

    private async Task<string> DiscoverEndpoint()
    {
        var candidates = CreateDiscoveryCandidates();
        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Options, candidate);
                request.Headers.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
                request.Headers.TryAddWithoutValidation("MS-ASProtocolVersion", DefaultProtocolVersion);
                request.Headers.TryAddWithoutValidation("Authorization", BasicAuthHeader());
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var response = await _http.SendAsync(request, cts.Token);
                var status = (int)response.StatusCode;
                if (status is 200 or 401 or 403 or 451) return candidate;
            }
            catch { }
        }
        return candidates.FirstOrDefault() ?? throw ExchangeException.ActiveSync("Не удалось определить ActiveSync endpoint.");
    }

    private List<string> CreateDiscoveryCandidates()
    {
        if (!string.IsNullOrEmpty(_settings.ActiveSyncEndpoint)) return [_settings.ActiveSyncEndpoint];
        var candidates = new List<string>();
        var domain = string.IsNullOrEmpty(_settings.Server)
            ? (_settings.Email.Contains('@') ? _settings.Email.Split('@')[^1] : "")
            : _settings.Server;
        if (string.IsNullOrEmpty(domain)) return candidates;
        candidates.Add(DefaultEndpoint(domain));
        if (!domain.StartsWith("autodiscover.")) candidates.Add(DefaultEndpoint($"autodiscover.{domain}"));
        if (!domain.StartsWith("mail.")) candidates.Add(DefaultEndpoint($"mail.{domain}"));
        return candidates.Distinct().ToList();
    }

    private static string DefaultEndpoint(string host) => $"https://{host}/Microsoft-Server-ActiveSync";

    private async Task<string> ExecuteCommand(string command, string xml, string? policyKeyOverride = null, string protocolVersion = DefaultProtocolVersion)
    {
        await EnsureEndpoint();
        var body = WbxmlCodec.Encode(xml);
        var deviceId = _settings.ResolvedDeviceId;
        var user = _settings.ActiveSyncUserParam;
        if (string.IsNullOrEmpty(deviceId)) throw ExchangeException.ActiveSync("DeviceId отсутствует.");
        if (string.IsNullOrEmpty(user)) throw ExchangeException.ActiveSync("Укажите email для синхронизации.");

        var uri = $"{_endpoint}?Cmd={Uri.EscapeDataString(command)}&User={Uri.EscapeDataString(user)}&DeviceId={Uri.EscapeDataString(deviceId)}&DeviceType={Uri.EscapeDataString(DefaultDeviceType)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-sync.wbxml");
        request.Headers.TryAddWithoutValidation("Authorization", BasicAuthHeader());
        request.Headers.TryAddWithoutValidation("MS-ASProtocolVersion", protocolVersion);
        request.Headers.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
        request.Headers.TryAddWithoutValidation("X-MS-PolicyKey", policyKeyOverride ?? _policyKey);

        HttpResponseMessage response;
        try { response = await _http.SendAsync(request); }
        catch (Exception ex) { throw ExchangeException.ActiveSync(ex.Message); }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is < 200 or >= 300)
            {
                if (status == 401) throw ExchangeException.Unauthorized();
                throw ExchangeException.ActiveSync($"ActiveSync {command} failed with HTTP {status} ({ClassifyHttpError(status)})");
            }
            var data = await response.Content.ReadAsByteArrayAsync();
            return WbxmlCodec.Decode(data);
        }
    }

    private string BasicAuthHeader()
    {
        var credentials = $"{_settings.ActiveSyncAuthUsername}:{_password}";
        return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
    }

    private static string ClassifyHttpError(int status) => status switch
    {
        401 => "bad-credentials",
        403 => "device-blocked",
        404 => "endpoint-not-found",
        _ => status >= 400 ? "protocol-error" : "unknown"
    };

    private static string ActiveSyncStatusHint(string status) => status switch
    {
        "108" => "Некорректный DeviceId. Попробуйте перезапустить приложение.",
        "109" => "Некорректный тип устройства.",
        "142" or "144" => "Требуется повторная регистрация устройства.",
        "177" => "Достигнут лимит устройств на аккаунте. Удалите старые устройства в OWA.",
        _ => ""
    };
}
