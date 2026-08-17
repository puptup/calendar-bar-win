using System.Text.RegularExpressions;

namespace CalendarBar;

public sealed class ExchangeClient
{
    private readonly ActiveSyncClient _client;

    public ExchangeClient(AccountSettings settings, string password)
    {
        _client = new ActiveSyncClient(settings, password);
    }

    public string DeviceId => _client.CurrentDeviceId;

    public async Task<List<CalendarEvent>> FetchCalendarEvents(DateTime start, DateTime end, List<NormalizedCalendarEvent>? inboxInvitations = null)
    {
        var normalized = await _client.GetCalendarEvents(inboxInvitations: inboxInvitations);
        var expanded = RecurrenceExpander.Expand(normalized, start, end);
        return DedupeEvents(MapAndFilterEvents(expanded, start, end));
    }

    public Task TestConnection() => _client.TestConnection();

    public Task<MailSyncSnapshot> FetchMailMessages(MailFolderKind folder, bool forceFullResync = false) =>
        _client.GetMailMessages(folder, forceFullResync: forceFullResync);

    public async Task<MailBody?> FetchMessageBody(MailMessage message, MailFolderKind folder = MailFolderKind.Inbox)
    {
        var protocolVersion = await _client.MailProtocolVersion(folder);
        return await _client.FetchMessageBody(message.CollectionId, message.ServerId, protocolVersion: protocolVersion);
    }

    public Task<ItemOperationsFetchResult> FetchAttachment(MailAttachment attachment) =>
        _client.FetchAttachment(attachment.FileReference);

    public async Task SetMessagesRead(IEnumerable<MailMessage> messages, bool read)
    {
        const int batchSize = 50;
        foreach (var group in messages.GroupBy(m => m.CollectionId))
        {
            var serverIds = group.Select(m => m.ServerId).ToList();
            for (var i = 0; i < serverIds.Count; i += batchSize)
            {
                var chunk = serverIds.Skip(i).Take(batchSize).ToList();
                await _client.SetMessagesRead(group.Key, chunk, read);
            }
        }
    }

    public Task SetMessageRead(MailMessage message, bool read) =>
        _client.SetMessageRead(message.CollectionId, message.ServerId, read);

    public Task SendMail(List<MailAddress> to, List<MailAddress> cc, string subject, string body) =>
        _client.SendMail(to, cc, subject, body);

    public Task Reply(MailMessage message, string body, bool replyAll) =>
        _client.SmartReply(message, body, replyAll);

    public Task Forward(MailMessage message, List<MailAddress> to, string body) =>
        _client.SmartForward(message, to, body);

    public Task RespondToMeeting(string eventId, MeetingAction action) =>
        _client.RespondToMeeting(eventId, action);

    public Task DeleteCalendarEvent(string eventId) =>
        _client.DeleteCalendarEvent(eventId);

    private static List<CalendarEvent> MapAndFilterEvents(IEnumerable<NormalizedCalendarEvent> events, DateTime start, DateTime end) =>
        events.Select(eventItem =>
        {
            var startDate = ActiveSyncDateParser.Parse(eventItem.StartAt);
            var endDate = ActiveSyncDateParser.Parse(string.IsNullOrEmpty(eventItem.EndAt) ? eventItem.StartAt : eventItem.EndAt);
            if (startDate is null || endDate is null) return null;
            if (startDate >= end || endDate <= start) return null;
            var organizer = eventItem.Organizer is null ? null
                : string.IsNullOrEmpty(eventItem.Organizer.Name) ? eventItem.Organizer.Email : eventItem.Organizer.Name;
            return new CalendarEvent
            {
                Id = string.IsNullOrEmpty(eventItem.ServerId) ? eventItem.Uid : eventItem.ServerId,
                Subject = string.IsNullOrEmpty(eventItem.Title) ? "Без названия" : eventItem.Title,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Location = string.IsNullOrEmpty(eventItem.Location) ? null : eventItem.Location,
                Body = string.IsNullOrEmpty(eventItem.Description) ? null : eventItem.Description,
                Organizer = organizer,
                Attendees = eventItem.Attendees.Select(a => new EventAttendee { Name = a.Name, Email = a.Email, Role = a.Role }).ToList(),
                IsAllDay = eventItem.AllDay,
                ReminderMinutes = eventItem.ReminderMinutes,
                ResponseStatus = eventItem.ResponseStatus,
                IsCancelled = eventItem.IsCancelled,
                IsRecurring = eventItem.IsRecurring,
                SourceTimeZone = string.IsNullOrEmpty(eventItem.TimeZone) ? null : eventItem.TimeZone,
                Source = eventItem.Source
            };
        }).OfType<CalendarEvent>().OrderBy(e => e.StartDate).ToList();

    private static List<CalendarEvent> DedupeEvents(List<CalendarEvent> events)
    {
        var deduped = new List<CalendarEvent>();
        foreach (var eventItem in events)
        {
            var index = deduped.FindIndex(e => CalendarEventsMatch(e, eventItem));
            if (index >= 0)
            {
                if (EventQuality(eventItem) > EventQuality(deduped[index]))
                    deduped[index] = eventItem;
            }
            else deduped.Add(eventItem);
        }
        return deduped.OrderBy(e => e.StartDate).ToList();
    }

    private static bool CalendarEventsMatch(CalendarEvent lhs, CalendarEvent rhs) =>
        NormalizedCalendarTitle(lhs.Subject) == NormalizedCalendarTitle(rhs.Subject)
        && Math.Abs((lhs.StartDate - rhs.StartDate).TotalSeconds) <= 120
        && Math.Abs((lhs.EndDate - rhs.EndDate).TotalSeconds) <= 120;

    private static int EventQuality(CalendarEvent eventItem)
    {
        var score = 0;
        if (!string.IsNullOrEmpty(eventItem.Body)) score += 1;
        if (!string.IsNullOrEmpty(eventItem.Location)) score += 1;
        if (!string.IsNullOrEmpty(eventItem.Organizer)) score += 2;
        score += Math.Min(eventItem.Attendees.Count, 10) * 2;
        if (eventItem.ResponseStatus != MeetingResponseStatus.Pending) score += 1;
        return score;
    }

    private static string NormalizedCalendarTitle(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
}
