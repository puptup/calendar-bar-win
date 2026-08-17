using System.Windows.Threading;

namespace CalendarBar;

public enum NotificationAuthorizationState { Authorized, Denied, NotDetermined }

public sealed class NotificationService : ObservableObject
{
    public static NotificationService Shared { get; } = new();
    private const string DeliveredReminderKeysKey = "deliveredCalendarReminderKeys";

    private NotificationAuthorizationState _authorizationState = NotificationAuthorizationState.NotDetermined;
    private bool _isAuthorized;
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private List<(CalendarEvent Event, int MinutesBefore, DateTime NotifyAt, string Key)> _pending = [];

    public NotificationAuthorizationState AuthorizationState
    {
        get => _authorizationState;
        private set => SetProperty(ref _authorizationState, value);
    }

    public bool IsAuthorized
    {
        get => _isAuthorized;
        private set => SetProperty(ref _isAuthorized, value);
    }

    public void Configure()
    {
        _reminderTimer.Tick += (_, _) => DrainDueReminders();
        _reminderTimer.Start();
        RefreshAuthorizationStatus();
    }

    public void RefreshAuthorizationStatus()
    {
        AuthorizationState = NotificationAuthorizationState.Authorized;
        IsAuthorized = true;
    }

    public Task RequestAuthorization()
    {
        RefreshAuthorizationStatus();
        return Task.CompletedTask;
    }

    public void OpenSystemNotificationSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "ms-settings:notifications") { UseShellExecute = true });
        }
        catch { }
    }

    public Task ScheduleNotifications(IEnumerable<CalendarEvent> events, int minutesBefore)
    {
        var now = DateTime.Now;
        var delivered = DeliveredReminderKeys();
        var scheduledKeys = new HashSet<string>();
        var pending = new List<(CalendarEvent, int, DateTime, string)>();
        foreach (var eventItem in events.Where(e => e.StartDate > now && !e.IsCancelled))
        {
            var notifyAt = eventItem.StartDate.AddMinutes(-minutesBefore);
            var key = ReminderKey(eventItem, minutesBefore);
            if (!scheduledKeys.Add(key)) continue;
            if (notifyAt <= now && delivered.Contains(key)) continue;
            if (notifyAt <= now && eventItem.StartDate <= now) continue;
            pending.Add((eventItem, minutesBefore, notifyAt <= now ? now.AddSeconds(1) : notifyAt, key));
        }
        _pending = pending;
        return Task.CompletedTask;
    }

    public Task RescheduleFromCurrentEvents() =>
        ScheduleNotifications(CalendarSyncService.Shared.Events, SettingsStore.Shared.NotifyMinutesBefore);

    public Task DeliverNewMailNotification(MailMessage message)
    {
        if (!IsAuthorized) throw ExchangeException.ActiveSync("Уведомления CalendarBar не разрешены в Windows.");
        TrayManager.Shared.ShowBalloon(
            message.DisplaySubject,
            $"{message.From?.DisplayName ?? "Новое письмо"}\n{MailNotificationBody(message)}",
            message.Id);
        return Task.CompletedTask;
    }

    private void DrainDueReminders()
    {
        var now = DateTime.Now;
        var due = _pending.Where(p => p.NotifyAt <= now).ToList();
        if (due.Count == 0) return;
        _pending = _pending.Where(p => p.NotifyAt > now).ToList();
        foreach (var (eventItem, minutesBefore, _, key) in due)
        {
            ShowEventToast(eventItem, minutesBefore);
            MarkReminderDelivered(key);
        }
    }

    private static void ShowEventToast(CalendarEvent eventItem, int minutesBefore)
    {
        var parts = new List<string> { $"Через {minutesBefore} мин · {eventItem.DurationText}" };
        if (!string.IsNullOrEmpty(eventItem.Location)) parts.Add(eventItem.Location);
        TrayManager.Shared.ShowBalloon(eventItem.Subject, string.Join("\n", parts));
    }

    private static string ReminderKey(CalendarEvent eventItem, int minutesBefore)
    {
        var start = new DateTimeOffset(eventItem.StartDate).ToUnixTimeSeconds();
        var subject = System.Text.RegularExpressions.Regex.Replace(eventItem.Subject.Trim().ToLowerInvariant(), @"\s+", " ");
        var location = System.Text.RegularExpressions.Regex.Replace((eventItem.Location ?? "").Trim().ToLowerInvariant(), @"\s+", " ");
        return $"{start}|{minutesBefore}|{subject[..Math.Min(120, subject.Length)]}|{location[..Math.Min(80, location.Length)]}";
    }

    private static HashSet<string> DeliveredReminderKeys() =>
        AppData.GetStringArray(DeliveredReminderKeysKey).ToHashSet();

    private static void MarkReminderDelivered(string key)
    {
        var keys = DeliveredReminderKeys();
        keys.Add(key);
        AppData.SetStringArray(DeliveredReminderKeysKey, keys);
    }

    private static string MailNotificationBody(MailMessage message)
    {
        var text = message.PreviewText.Trim();
        if (string.IsNullOrEmpty(text)) return "Откройте письмо в CalendarBar";
        return text.Length > 180 ? text[..180] : text;
    }
}
