using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace CalendarBar;

public sealed class CalendarSyncService : ObservableObject
{
    public static CalendarSyncService Shared { get; } = new();

    private List<CalendarEvent> _events = [];
    private List<CalendarEvent> _todayEvents = [];
    private SyncState _syncState = new SyncState.Idle();
    private DateTime _statusRefreshTick = DateTime.Now;
    private DispatcherTimer? _syncTimer;
    private DispatcherTimer? _menuBarRefreshTimer;
    private bool _syncInProgress;
    private bool _pendingSyncRequested;
    private const string SuppressedInboxInvitationKeysKey = "suppressedInboxInvitationKeys";

    public List<CalendarEvent> Events
    {
        get => _events;
        private set => SetProperty(ref _events, value);
    }

    public List<CalendarEvent> TodayEvents
    {
        get => _todayEvents;
        private set => SetProperty(ref _todayEvents, value);
    }

    public SyncState SyncState
    {
        get => _syncState;
        private set => SetProperty(ref _syncState, value);
    }

    public DateTime StatusRefreshTick
    {
        get => _statusRefreshTick;
        private set => SetProperty(ref _statusRefreshTick, value);
    }

    private CalendarSyncService()
    {
        SettingsStore.Shared.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsStore.IsLoggedIn))
            {
                if (SettingsStore.Shared.IsLoggedIn) StartPeriodicSync();
                else
                {
                    StopPeriodicSync();
                    Events = [];
                    TodayEvents = [];
                    TrayManager.Shared.UpdateCalendarTitle("");
                }
                TrayManager.Shared.UpdateCalendarIcon();
            }
            if (e.PropertyName is nameof(SettingsStore.NotifyMinutesBefore))
                _ = NotificationService.Shared.RescheduleFromCurrentEvents();
            if (e.PropertyName is nameof(SettingsStore.SyncIntervalMinutes) && SettingsStore.Shared.IsLoggedIn)
                StartPeriodicSync();
        };
        if (SettingsStore.Shared.IsLoggedIn) StartPeriodicSync();
    }

    public void StartPeriodicSync()
    {
        StopPeriodicSync();
        _syncTimer = new DispatcherTimer { Interval = SyncInterval };
        _syncTimer.Tick += async (_, _) => await SyncNow();
        _syncTimer.Start();
        _menuBarRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _menuBarRefreshTimer.Tick += (_, _) => RefreshMenuBarTitle();
        _menuBarRefreshTimer.Start();
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(SyncNow);
        });
    }

    public void StopPeriodicSync()
    {
        _syncTimer?.Stop();
        _syncTimer = null;
        _menuBarRefreshTimer?.Stop();
        _menuBarRefreshTimer = null;
    }

    private static TimeSpan SyncInterval
    {
        get
        {
            var minutes = SettingsStore.Shared.SyncIntervalMinutes;
            return minutes <= 0 ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(minutes);
        }
    }

    public async Task SyncNow()
    {
        var store = SettingsStore.Shared;
        if (!store.IsLoggedIn || store.Password is null) return;
        if (_syncInProgress)
        {
            _pendingSyncRequested = true;
            return;
        }
        _syncInProgress = true;
        try
        {
            SyncState = new SyncState.Syncing();
            var account = store.Account;
            var accountKey = account.Email.Trim().ToLowerInvariant();
            ActiveSyncSyncKeyStore.Shared.UpdateCalendar("0", accountKey);
            var start = DateTime.Today;
            var end = start.AddDays(60);
            var invitations = store.MailEnabled ? MailSyncService.Shared.InboxInvitations : null;
            try
            {
                var password = store.Password;
                var fetched = await new ExchangeClient(account, password).FetchCalendarEvents(start, end, invitations);
                var visible = ApplyDeletionSuppression(fetched);
                var today = visible.Where(e => e.Occurs(DateTime.Now)).ToList();
                Events = visible.Where(e => e.IsUpcoming).ToList();
                TodayEvents = today;
                SyncState = new SyncState.Success(DateTime.Now);
                RefreshMenuBarTitle();
                await NotificationService.Shared.ScheduleNotifications(Events, store.NotifyMinutesBefore);
            }
            catch (Exception ex)
            {
                SyncState = new SyncState.Failure(ex.Message);
            }
        }
        finally
        {
            _syncInProgress = false;
            if (_pendingSyncRequested)
            {
                _pendingSyncRequested = false;
                await SyncNow();
            }
        }
    }

    public int TodayMeetingCount => TodayEvents.Count(e => e.IsUpcoming);

    public CalendarEvent? NextTodayUpcoming =>
        TodayEvents.Where(e => e.IsFuture).OrderBy(e => e.StartDate).FirstOrDefault();

    public string? FooterStatusText
    {
        get
        {
            var next = NextTodayUpcoming;
            if (next is null) return null;
            if (next.IsAllDay) return "Следующая встреча: весь день";
            return $"Следующая встреча в {next.StartDate.ToString("HH:mm", new CultureInfo("ru-RU"))}";
        }
    }

    public bool MenuBarShowsSummary => TodayMeetingCount > 0 && NextTodayUpcoming is not null;

    public string MenuBarCountText => TodayMeetingCount > 0 ? $"{TodayMeetingCount}" : "";

    public string MenuBarTimeText
    {
        get
        {
            var next = NextTodayUpcoming;
            if (next is null) return "";
            if (next.IsAllDay) return "весь день";
            return next.StartDate.ToString("HH:mm", new CultureInfo("ru-RU"));
        }
    }

    public string MenuBarLabelText => MenuBarShowsSummary ? $"{MenuBarCountText} · {MenuBarTimeText}" : "";

    public void RefreshMenuBarTitle()
    {
        StatusRefreshTick = DateTime.Now;
        TrayManager.Shared.UpdateCalendarTitle(MenuBarShowsSummary ? MenuBarLabelText : "");
        OnPropertyChanged(nameof(FooterStatusText));
        OnPropertyChanged(nameof(MenuBarLabelText));
    }

    public async Task Respond(CalendarEvent eventItem, MeetingAction action)
    {
        var store = SettingsStore.Shared;
        if (!store.IsLoggedIn || store.Password is null) return;
        await new ExchangeClient(store.Account, store.Password).RespondToMeeting(eventItem.Id, action);
        await SyncNow();
    }

    public async Task Delete(CalendarEvent eventItem)
    {
        var store = SettingsStore.Shared;
        if (!store.IsLoggedIn || store.Password is null) return;
        await new ExchangeClient(store.Account, store.Password).DeleteCalendarEvent(eventItem.Id);
        SuppressInboxInvitation(eventItem);
        Events = Events.Where(e => e.Id != eventItem.Id).ToList();
        TodayEvents = TodayEvents.Where(e => e.Id != eventItem.Id).ToList();
        await SyncNow();
    }

    private List<CalendarEvent> ApplyDeletionSuppression(List<CalendarEvent> fetched)
    {
        var previous = Events.Where(e => e.Source == EventSource.Calendar).Select(EventSemanticKey).ToHashSet();
        var fetchedKeys = fetched.Where(e => e.Source == EventSource.Calendar).Select(EventSemanticKey).ToHashSet();
        var removed = previous.Except(fetchedKeys).ToHashSet();
        var suppressed = SuppressedInboxInvitationKeys();
        suppressed.UnionWith(removed);
        suppressed.ExceptWith(fetchedKeys);
        SaveSuppressedInboxInvitationKeys(suppressed);
        return fetched.Where(e => e.Source != EventSource.InboxInvitation || !suppressed.Contains(EventSemanticKey(e))).ToList();
    }

    private void SuppressInboxInvitation(CalendarEvent eventItem)
    {
        var suppressed = SuppressedInboxInvitationKeys();
        suppressed.Add(EventSemanticKey(eventItem));
        SaveSuppressedInboxInvitationKeys(suppressed);
    }

    private static HashSet<string> SuppressedInboxInvitationKeys() =>
        AppData.GetStringArray(SuppressedInboxInvitationKeysKey).ToHashSet();

    private static void SaveSuppressedInboxInvitationKeys(HashSet<string> keys) =>
        AppData.SetStringArray(SuppressedInboxInvitationKeysKey, keys);

    private static string EventSemanticKey(CalendarEvent eventItem)
    {
        var subject = Regex.Replace(eventItem.Subject.Trim().ToLowerInvariant(), @"\s+", " ");
        return $"{new DateTimeOffset(eventItem.StartDate).ToUnixTimeSeconds()}|{new DateTimeOffset(eventItem.EndDate).ToUnixTimeSeconds()}|{subject}";
    }
}
