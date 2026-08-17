using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CalendarBar;

public sealed class FolderRecord
{
    public string ServerId { get; init; } = "";
    public string? ParentId { get; init; }
    public string DisplayName { get; init; } = "";
    public string Type { get; init; } = "";
}

public sealed class FolderSyncResult
{
    public string SyncKey { get; init; } = "";
    public string Status { get; init; } = "";
    public List<FolderRecord> Folders { get; init; } = [];
}

public sealed class CalendarOrganizer
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
}

public sealed class CalendarAttendee
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "required";
}

public sealed class CalendarRecurrence
{
    public string Type { get; init; } = "";
    public int Interval { get; init; } = 1;
    public int? Occurrences { get; init; }
    public string Until { get; init; } = "";
    public int? DayOfWeek { get; init; }
    public int? DayOfMonth { get; init; }
    public int? WeekOfMonth { get; init; }
    public int? MonthOfYear { get; init; }
}

public sealed class CalendarException
{
    public bool Deleted { get; init; }
    public string ExceptionStartAt { get; init; } = "";
    public string StartAt { get; init; } = "";
    public string EndAt { get; init; } = "";
    public string Title { get; init; } = "";
    public string Location { get; init; } = "";
    public bool? AllDay { get; init; }
    public string Description { get; init; } = "";
}

public sealed class NormalizedCalendarEvent
{
    public string ServerId { get; init; } = "";
    public string Uid { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Location { get; init; } = "";
    public string StartAt { get; init; } = "";
    public string EndAt { get; init; } = "";
    public bool AllDay { get; init; }
    public string? TimeZone { get; init; }
    public CalendarRecurrence? Recurrence { get; init; }
    public List<CalendarException> Exceptions { get; init; } = [];
    public List<CalendarAttendee> Attendees { get; init; } = [];
    public CalendarOrganizer? Organizer { get; init; }
    public int? ReminderMinutes { get; init; }
    public MeetingResponseStatus ResponseStatus { get; init; }
    public EventSource Source { get; init; }
    public string InstanceType { get; init; } = "";
    public string MeetingStatus { get; init; } = "";
    public bool IsCancelled { get; init; }
    public bool IsRecurring { get; init; }

    public NormalizedCalendarEvent AsInstance(
        string serverId, string startAt, string endAt,
        string? title = null, string? location = null, string? description = null, bool? allDay = null) => new()
    {
        ServerId = serverId,
        Uid = Uid,
        Title = title ?? Title,
        Description = description ?? Description,
        Location = location ?? Location,
        StartAt = startAt,
        EndAt = endAt,
        AllDay = allDay ?? AllDay,
        TimeZone = TimeZone,
        Recurrence = null,
        Exceptions = [],
        Attendees = Attendees,
        Organizer = Organizer,
        ReminderMinutes = ReminderMinutes,
        ResponseStatus = ResponseStatus,
        Source = Source,
        InstanceType = "2",
        MeetingStatus = MeetingStatus,
        IsCancelled = IsCancelled,
        IsRecurring = true
    };
}

public sealed class ParsedInboxMeetingRequest : IEquatable<ParsedInboxMeetingRequest>
{
    public string ServerId { get; init; } = "";
    public string Subject { get; init; } = "";
    public string From { get; init; } = "";
    public string StartTime { get; init; } = "";
    public string EndTime { get; init; } = "";
    public string Location { get; init; } = "";
    public string AllDayEvent { get; init; } = "";
    public string GlobalObjId { get; init; } = "";
    public string BodyData { get; init; } = "";
    public string Read { get; init; } = "";

    public bool Equals(ParsedInboxMeetingRequest? other) => other is not null && ServerId == other.ServerId;
    public override bool Equals(object? obj) => Equals(obj as ParsedInboxMeetingRequest);
    public override int GetHashCode() => ServerId.GetHashCode();
}

public sealed class InboxSyncResult
{
    public string SyncKey { get; init; } = "";
    public string Status { get; init; } = "";
    public bool MoreAvailable { get; init; }
    public List<ParsedInboxMeetingRequest> MeetingRequests { get; init; } = [];
    public List<MailMessage> Messages { get; init; } = [];
    public List<string> DeletedServerIds { get; init; } = [];
}

public sealed class ItemOperationsFetchResult
{
    public string Status { get; init; } = "";
    public MailBody? Body { get; init; }
    public List<MailAttachment> Attachments { get; init; } = [];
    public byte[]? Data { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
}

public sealed class CommandStatusResult
{
    public string Status { get; init; } = "";
    public string SyncKey { get; init; } = "";
    public Dictionary<string, string> ItemStatuses { get; init; } = [];
}

public sealed class ParsedCalendarEvent
{
    public string ServerId { get; init; } = "";
    public ParsedCalendarApplicationData ApplicationData { get; init; } = new();
}

public sealed class ParsedCalendarApplicationData
{
    public string Subject { get; init; } = "";
    public string StartTime { get; init; } = "";
    public string EndTime { get; init; } = "";
    public string Location { get; init; } = "";
    public string Uid { get; init; } = "";
    public string AllDayEvent { get; init; } = "";
    public string TimeZone { get; init; } = "";
    public string BodyType { get; init; } = "";
    public string BodyData { get; init; } = "";
    public string Reminder { get; init; } = "";
    public ParsedCalendarRecurrence? Recurrence { get; init; }
    public string OrganizerName { get; init; } = "";
    public string OrganizerEmail { get; init; } = "";
    public string MeetingStatus { get; init; } = "";
    public string ResponseType { get; init; } = "";
    public List<(string Name, string Email, string Type)> Attendees { get; init; } = [];
    public List<ParsedCalendarException> Exceptions { get; init; } = [];
    public string InstanceType { get; init; } = "";
}

public sealed class ParsedCalendarRecurrence
{
    public string Type { get; init; } = "";
    public string Interval { get; init; } = "";
    public string Occurrences { get; init; } = "";
    public string Until { get; init; } = "";
    public string DayOfWeek { get; init; } = "";
    public string DayOfMonth { get; init; } = "";
    public string WeekOfMonth { get; init; } = "";
    public string MonthOfYear { get; init; } = "";
}

public sealed class ParsedCalendarException
{
    public bool Deleted { get; init; }
    public string ExceptionStartTime { get; init; } = "";
    public string StartTime { get; init; } = "";
    public string EndTime { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Location { get; init; } = "";
    public string AllDayEvent { get; init; } = "";
    public string BodyType { get; init; } = "";
    public string BodyData { get; init; } = "";
}

public sealed class CalendarSyncResult
{
    public string SyncKey { get; init; } = "";
    public string Status { get; init; } = "";
    public bool MoreAvailable { get; init; }
    public List<ParsedCalendarEvent> Events { get; init; } = [];
    public List<string> DeletedServerIds { get; init; } = [];
}

public sealed class ProvisionResponse
{
    public string Status { get; init; } = "";
    public string PolicyType { get; init; } = "";
    public string PolicyStatus { get; init; } = "";
    public string PolicyKey { get; init; } = "";
    public bool RemoteWipe { get; init; }
}

public sealed class ProvisionRequestConfig
{
    public string DeviceModel { get; init; } = "";
    public string DeviceImei { get; init; } = "";
    public string DeviceFriendlyName { get; init; } = "";
    public string DeviceOs { get; init; } = "";
    public string DeviceOsLanguage { get; init; } = "";
    public string DevicePhoneNumber { get; init; } = "";
    public string DeviceMobileOperator { get; init; } = "";
    public string UserAgent { get; init; } = "";
}

public static class ActiveSyncParser
{
    public static string GetFirstTagText(string xml, string tagName)
    {
        var match = Regex.Match(xml, $"<{tagName}(?:\\s[^>]*)?>([\\s\\S]*?)</{tagName}>", RegexOptions.IgnoreCase);
        return match.Success ? DecodeXml(match.Groups[1].Value.Trim()) : "";
    }

    public static List<string> GetAllTagBlocks(string xml, string tagName)
    {
        var matches = Regex.Matches(xml, $"<{tagName}(?:\\s[^>]*)?>([\\s\\S]*?)</{tagName}>", RegexOptions.IgnoreCase);
        return matches.Select(m => m.Groups[1].Value).ToList();
    }

    public static bool HasSelfClosingTag(string xml, string tagName) =>
        Regex.IsMatch(xml, $"<{tagName}(?:\\s[^>]*)?\\s*/>", RegexOptions.IgnoreCase);

    public static string DecodeXml(string value) =>
        value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");

    public static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

    public static FolderSyncResult ParseFolderSyncXml(string xml)
    {
        var addBlocks = GetAllTagBlocks(xml, "Add");
        return new FolderSyncResult
        {
            SyncKey = GetFirstTagText(xml, "SyncKey"),
            Status = GetFirstTagText(xml, "Status"),
            Folders = addBlocks.Select(block => new FolderRecord
            {
                ServerId = GetFirstTagText(block, "ServerId"),
                ParentId = string.IsNullOrEmpty(GetFirstTagText(block, "ParentId")) ? null : GetFirstTagText(block, "ParentId"),
                DisplayName = GetFirstTagText(block, "DisplayName"),
                Type = GetFirstTagText(block, "Type")
            }).ToList()
        };
    }

    public static FolderRecord? FindCalendarFolder(IEnumerable<FolderRecord> folders) =>
        folders.FirstOrDefault(f => f.Type == "8")
        ?? folders.FirstOrDefault(f => f.DisplayName.Trim().Equals("calendar", StringComparison.OrdinalIgnoreCase))
        ?? folders.FirstOrDefault(f => f.ServerId.Contains("calendar", StringComparison.OrdinalIgnoreCase));

    public static FolderRecord? FindInboxFolder(IEnumerable<FolderRecord> folders) =>
        folders.FirstOrDefault(f => f.Type == "2")
        ?? folders.FirstOrDefault(f => MatchesFolderName(f.DisplayName, ["inbox", "входящие", "received", "boîte de réception", "posteingang"]))
        ?? folders.FirstOrDefault(f => MatchesFolderName(f.ServerId, ["inbox", "входящие"]));

    public static FolderRecord? FindMailFolder(IEnumerable<FolderRecord> folders, MailFolderKind kind) => kind switch
    {
        MailFolderKind.Inbox => FindInboxFolder(folders),
        MailFolderKind.Sent => folders.FirstOrDefault(f => f.Type == "5")
            ?? folders.FirstOrDefault(f => MatchesFolderName(f.DisplayName, ["sent", "sent items", "отправленные"])),
        MailFolderKind.Drafts => folders.FirstOrDefault(f => f.Type == "3")
            ?? folders.FirstOrDefault(f => MatchesFolderName(f.DisplayName, ["drafts", "черновики"])),
        MailFolderKind.Trash => folders.FirstOrDefault(f => f.Type == "4")
            ?? folders.FirstOrDefault(f => MatchesFolderName(f.DisplayName, ["deleted", "deleted items", "trash", "корзина", "удаленные", "удалённые"])),
        _ => null
    };

    private static bool MatchesFolderName(string displayName, string[] names)
    {
        var normalized = displayName.Trim().ToLowerInvariant();
        return names.Any(n => normalized == n || normalized.Contains(n));
    }

    public static string BuildFolderSyncRequestXml(string syncKey = "0") =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><FolderSync xmlns=\"FolderHierarchy:\"><SyncKey>{EscapeXml(syncKey)}</SyncKey></FolderSync>";

    public static CalendarSyncResult ParseCalendarSyncXml(string xml)
    {
        var collectionBlock = GetAllTagBlocks(xml, "Collection").FirstOrDefault() ?? "";
        var commandBlocks = GetAllTagBlocks(collectionBlock, "Add").Concat(GetAllTagBlocks(collectionBlock, "Change"));
        var deleted = GetAllTagBlocks(collectionBlock, "Delete").Concat(GetAllTagBlocks(collectionBlock, "SoftDelete"))
            .Select(b => GetFirstTagText(b, "ServerId")).Where(s => s.Length > 0).ToList();
        var status = GetFirstTagText(collectionBlock, "Status");
        if (string.IsNullOrEmpty(status)) status = GetFirstTagText(xml, "Status");
        return new CalendarSyncResult
        {
            SyncKey = GetFirstTagText(collectionBlock, "SyncKey"),
            Status = status,
            MoreAvailable = HasSelfClosingTag(collectionBlock, "MoreAvailable"),
            Events = commandBlocks.Select(ParseCalendarSyncCommand).OfType<ParsedCalendarEvent>().ToList(),
            DeletedServerIds = deleted
        };
    }

    private static ParsedCalendarEvent? ParseCalendarSyncCommand(string xml)
    {
        var itemStatus = GetFirstTagText(xml, "Status");
        if (!string.IsNullOrEmpty(itemStatus) && itemStatus != "1") return null;
        var applicationData = GetAllTagBlocks(xml, "ApplicationData").FirstOrDefault() ?? "";
        var bodyBlock = GetAllTagBlocks(applicationData, "Body").FirstOrDefault() ?? "";
        var recurrenceBlock = GetAllTagBlocks(applicationData, "Recurrence").FirstOrDefault() ?? "";
        var exceptionBlocks = GetAllTagBlocks(applicationData, "Exception");
        ParsedCalendarRecurrence? recurrence = string.IsNullOrEmpty(recurrenceBlock) ? null : new ParsedCalendarRecurrence
        {
            Type = GetFirstTagText(recurrenceBlock, "Type"),
            Interval = GetFirstTagText(recurrenceBlock, "Interval"),
            Occurrences = GetFirstTagText(recurrenceBlock, "Occurrences"),
            Until = GetFirstTagText(recurrenceBlock, "Until"),
            DayOfWeek = GetFirstTagText(recurrenceBlock, "DayOfWeek"),
            DayOfMonth = GetFirstTagText(recurrenceBlock, "DayOfMonth"),
            WeekOfMonth = GetFirstTagText(recurrenceBlock, "WeekOfMonth"),
            MonthOfYear = GetFirstTagText(recurrenceBlock, "MonthOfYear")
        };
        return new ParsedCalendarEvent
        {
            ServerId = GetFirstTagText(xml, "ServerId"),
            ApplicationData = new ParsedCalendarApplicationData
            {
                Subject = GetFirstTagText(applicationData, "Subject"),
                StartTime = GetFirstTagText(applicationData, "StartTime"),
                EndTime = GetFirstTagText(applicationData, "EndTime"),
                Location = GetFirstTagText(applicationData, "Location"),
                Uid = GetFirstTagText(applicationData, "UID"),
                AllDayEvent = GetFirstTagText(applicationData, "AllDayEvent"),
                TimeZone = GetFirstTagText(applicationData, "TimeZone"),
                BodyType = GetFirstTagText(bodyBlock, "Type"),
                BodyData = GetFirstTagText(bodyBlock, "Data"),
                Reminder = GetFirstTagText(applicationData, "Reminder"),
                Recurrence = recurrence,
                OrganizerName = GetFirstTagText(applicationData, "OrganizerName"),
                OrganizerEmail = GetFirstTagText(applicationData, "OrganizerEmail"),
                MeetingStatus = GetFirstTagText(applicationData, "MeetingStatus"),
                ResponseType = GetFirstTagText(applicationData, "ResponseType"),
                Attendees = GetAllTagBlocks(applicationData, "Attendee").Select(block => (
                    GetFirstTagText(block, "Name"), GetFirstTagText(block, "Email"), GetFirstTagText(block, "AttendeeType")
                )).ToList(),
                Exceptions = exceptionBlocks.Select(block =>
                {
                    var exceptionBody = GetAllTagBlocks(block, "Body").FirstOrDefault() ?? "";
                    return new ParsedCalendarException
                    {
                        Deleted = GetFirstTagText(block, "Deleted") == "1",
                        ExceptionStartTime = GetFirstTagText(block, "ExceptionStartTime"),
                        StartTime = GetFirstTagText(block, "StartTime"),
                        EndTime = GetFirstTagText(block, "EndTime"),
                        Subject = GetFirstTagText(block, "Subject"),
                        Location = GetFirstTagText(block, "Location"),
                        AllDayEvent = GetFirstTagText(block, "AllDayEvent"),
                        BodyType = GetFirstTagText(exceptionBody, "Type"),
                        BodyData = GetFirstTagText(exceptionBody, "Data")
                    };
                }).ToList(),
                InstanceType = GetFirstTagText(applicationData, "InstanceType")
            }
        };
    }

    public static string BuildCalendarSyncRequestXml(string protocolVersion, string syncKey, string collectionId, int windowSize = 50)
    {
        var classElement = UsesLegacyClass(protocolVersion) ? "<Class>Calendar</Class>" : "";
        if (syncKey == "0")
            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\"><Collections><Collection>{classElement}<SyncKey>{EscapeXml(syncKey)}</SyncKey><CollectionId>{EscapeXml(collectionId)}</CollectionId></Collection></Collections></Sync>";
        return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\" xmlns:airsyncbase=\"AirSyncBase:\"><Collections><Collection>{classElement}<SyncKey>{EscapeXml(syncKey)}</SyncKey><CollectionId>{EscapeXml(collectionId)}</CollectionId><DeletesAsMoves>0</DeletesAsMoves><GetChanges>1</GetChanges><WindowSize>{windowSize}</WindowSize><Options><FilterType>5</FilterType><airsyncbase:BodyPreference><airsyncbase:Type>1</airsyncbase:Type><airsyncbase:TruncationSize>32768</airsyncbase:TruncationSize></airsyncbase:BodyPreference></Options></Collection></Collections></Sync>";
    }

    public static List<NormalizedCalendarEvent> NormalizeCalendarEvents(IEnumerable<ParsedCalendarEvent> events) =>
        events.Select(eventItem =>
        {
            var data = eventItem.ApplicationData;
            int? reminder = int.TryParse(data.Reminder, out var r) ? r : null;
            return new NormalizedCalendarEvent
            {
                ServerId = eventItem.ServerId,
                Uid = string.IsNullOrEmpty(data.Uid) ? eventItem.ServerId : data.Uid,
                Title = data.Subject,
                Description = data.BodyData,
                Location = data.Location,
                StartAt = NormalizeActiveSyncDateTime(data.StartTime),
                EndAt = NormalizeActiveSyncDateTime(data.EndTime),
                AllDay = data.AllDayEvent == "1",
                TimeZone = DecodeActiveSyncTimeZone(data.TimeZone),
                Recurrence = data.Recurrence is null ? null : new CalendarRecurrence
                {
                    Type = data.Recurrence.Type,
                    Interval = ParseInteger(data.Recurrence.Interval, 1),
                    Occurrences = ParseIntegerOptional(data.Recurrence.Occurrences),
                    Until = NormalizeActiveSyncDateTime(data.Recurrence.Until),
                    DayOfWeek = ParseIntegerOptional(data.Recurrence.DayOfWeek),
                    DayOfMonth = ParseIntegerOptional(data.Recurrence.DayOfMonth),
                    WeekOfMonth = ParseIntegerOptional(data.Recurrence.WeekOfMonth),
                    MonthOfYear = ParseIntegerOptional(data.Recurrence.MonthOfYear)
                },
                Exceptions = data.Exceptions.Select(ex => new CalendarException
                {
                    Deleted = ex.Deleted,
                    ExceptionStartAt = NormalizeActiveSyncDateTime(ex.ExceptionStartTime),
                    StartAt = NormalizeActiveSyncDateTime(ex.StartTime),
                    EndAt = NormalizeActiveSyncDateTime(ex.EndTime),
                    Title = ex.Subject,
                    Location = ex.Location,
                    AllDay = string.IsNullOrEmpty(ex.AllDayEvent) ? null : ex.AllDayEvent == "1",
                    Description = ex.BodyData
                }).ToList(),
                Attendees = data.Attendees.Select(a => new CalendarAttendee
                {
                    Name = a.Name, Email = a.Email, Role = NormalizeAttendeeType(a.Type)
                }).ToList(),
                Organizer = string.IsNullOrEmpty(data.OrganizerEmail) && string.IsNullOrEmpty(data.OrganizerName)
                    ? null
                    : new CalendarOrganizer { Name = data.OrganizerName, Email = data.OrganizerEmail },
                ReminderMinutes = reminder,
                ResponseStatus = MapResponseStatus(data.ResponseType, data.MeetingStatus),
                Source = EventSource.Calendar,
                InstanceType = data.InstanceType,
                MeetingStatus = data.MeetingStatus,
                IsCancelled = IsCancelledMeetingStatus(data.MeetingStatus),
                IsRecurring = data.Recurrence is not null || data.InstanceType is "2" or "3"
            };
        }).ToList();

    public static InboxSyncResult ParseInboxSyncXml(string xml, string collectionIdFallback = "")
    {
        var collectionBlock = GetAllTagBlocks(xml, "Collection").FirstOrDefault() ?? "";
        var collectionId = GetFirstTagText(collectionBlock, "CollectionId");
        if (string.IsNullOrEmpty(collectionId)) collectionId = collectionIdFallback;
        var commandBlocks = GetAllTagBlocks(collectionBlock, "Add").Concat(GetAllTagBlocks(collectionBlock, "Change")).ToList();
        var deletedBlocks = GetAllTagBlocks(collectionBlock, "Delete").Concat(GetAllTagBlocks(collectionBlock, "SoftDelete"));
        var status = GetFirstTagText(collectionBlock, "Status");
        if (string.IsNullOrEmpty(status)) status = GetFirstTagText(xml, "Status");
        return new InboxSyncResult
        {
            SyncKey = GetFirstTagText(collectionBlock, "SyncKey"),
            Status = status,
            MoreAvailable = HasSelfClosingTag(collectionBlock, "MoreAvailable"),
            MeetingRequests = commandBlocks.Select(ParseInboxMeetingRequest).OfType<ParsedInboxMeetingRequest>().ToList(),
            Messages = commandBlocks.Select(b => ParseMailMessage(b, collectionId)).OfType<MailMessage>().ToList(),
            DeletedServerIds = deletedBlocks.Select(b => GetFirstTagText(b, "ServerId")).Where(s => s.Length > 0).ToList()
        };
    }

    private static MailMessage? ParseMailMessage(string xml, string collectionId)
    {
        var itemStatus = GetFirstTagText(xml, "Status");
        if (!string.IsNullOrEmpty(itemStatus) && itemStatus != "1") return null;
        var serverId = GetFirstTagText(xml, "ServerId");
        if (string.IsNullOrEmpty(serverId)) return null;
        var applicationData = GetAllTagBlocks(xml, "ApplicationData").FirstOrDefault() ?? "";
        var bodyBlock = GetAllTagBlocks(applicationData, "Body").FirstOrDefault() ?? "";
        return new MailMessage
        {
            ServerId = serverId,
            CollectionId = collectionId,
            Subject = GetFirstTagText(applicationData, "Subject"),
            From = ParseMailAddress(GetFirstTagText(applicationData, "From")),
            To = ParseMailAddressList(GetFirstTagText(applicationData, "To")),
            Cc = ParseMailAddressList(GetFirstTagText(applicationData, "Cc")),
            ReplyTo = ParseMailAddressList(GetFirstTagText(applicationData, "ReplyTo")),
            DateReceived = ActiveSyncDateParser.Parse(NormalizeActiveSyncDateTime(GetFirstTagText(applicationData, "DateReceived"))),
            IsRead = GetFirstTagText(applicationData, "Read") == "1",
            Importance = string.IsNullOrEmpty(GetFirstTagText(applicationData, "Importance")) ? null : GetFirstTagText(applicationData, "Importance"),
            MessageClass = GetFirstTagText(applicationData, "MessageClass"),
            ConversationId = GetFirstTagText(applicationData, "ConversationId"),
            ConversationIndex = GetFirstTagText(applicationData, "ConversationIndex"),
            ThreadTopic = GetFirstTagText(applicationData, "ThreadTopic"),
            Body = ParseMailBody(bodyBlock),
            Preview = GetFirstTagText(bodyBlock, "Preview"),
            Attachments = ParseMailAttachments(applicationData)
        };
    }

    private static ParsedInboxMeetingRequest? ParseInboxMeetingRequest(string xml)
    {
        var applicationData = GetAllTagBlocks(xml, "ApplicationData").FirstOrDefault() ?? "";
        var messageClass = GetFirstTagText(applicationData, "MessageClass");
        if (!messageClass.Contains("Meeting.Request", StringComparison.OrdinalIgnoreCase)) return null;
        var meetingBlock = GetAllTagBlocks(applicationData, "MeetingRequest").FirstOrDefault() ?? applicationData;
        var bodyBlock = GetAllTagBlocks(applicationData, "Body").FirstOrDefault() ?? "";
        var startTime = GetFirstTagText(meetingBlock, "StartTime");
        if (string.IsNullOrEmpty(startTime)) return null;
        return new ParsedInboxMeetingRequest
        {
            ServerId = GetFirstTagText(xml, "ServerId"),
            Subject = GetFirstTagText(applicationData, "Subject"),
            From = GetFirstTagText(applicationData, "From"),
            StartTime = startTime,
            EndTime = GetFirstTagText(meetingBlock, "EndTime"),
            Location = GetFirstTagText(meetingBlock, "Location"),
            AllDayEvent = GetFirstTagText(meetingBlock, "AllDayEvent"),
            GlobalObjId = GetFirstTagText(meetingBlock, "GlobalObjId"),
            BodyData = GetFirstTagText(bodyBlock, "Data"),
            Read = GetFirstTagText(applicationData, "Read")
        };
    }

    public static string BuildInboxSyncRequestXml(string protocolVersion, string syncKey, string collectionId, int windowSize = 100, bool includeFilterType = true)
    {
        var classElement = UsesLegacyClass(protocolVersion) ? "<Class>Email</Class>" : "";
        if (syncKey == "0")
            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\"><Collections><Collection>{classElement}<SyncKey>{EscapeXml(syncKey)}</SyncKey><CollectionId>{EscapeXml(collectionId)}</CollectionId></Collection></Collections></Sync>";
        var filterElement = includeFilterType ? "<FilterType>5</FilterType>" : "";
        return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\" xmlns:airsyncbase=\"AirSyncBase:\"><Collections><Collection>{classElement}<SyncKey>{EscapeXml(syncKey)}</SyncKey><CollectionId>{EscapeXml(collectionId)}</CollectionId><DeletesAsMoves>0</DeletesAsMoves><GetChanges>1</GetChanges><WindowSize>{windowSize}</WindowSize><Options>{filterElement}<airsyncbase:BodyPreference><airsyncbase:Type>1</airsyncbase:Type><airsyncbase:TruncationSize>20000</airsyncbase:TruncationSize></airsyncbase:BodyPreference></Options></Collection></Collections></Sync>";
    }

    public static string BuildReadChangeRequestXml(string syncKey, string collectionId, IEnumerable<string> serverIds, bool read)
    {
        var changes = string.Concat(serverIds.Select(id =>
            $"<Change><ServerId>{EscapeXml(id)}</ServerId><ApplicationData><Read xmlns=\"Email:\">{(read ? "1" : "0")}</Read></ApplicationData></Change>"));
        return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\"><Collections><Collection><SyncKey>{EscapeXml(syncKey)}</SyncKey><CollectionId>{EscapeXml(collectionId)}</CollectionId><Commands>{changes}</Commands></Collection></Collections></Sync>";
    }

    public static string BuildItemOperationsFetchMessageXml(string collectionId, string serverId, MailBodyType bodyType = MailBodyType.Html)
    {
        var typeValue = bodyType == MailBodyType.Plain ? "1" : "2";
        return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><ItemOperations xmlns=\"ItemOperations:\" xmlns:airsync=\"AirSync:\" xmlns:airsyncbase=\"AirSyncBase:\"><Fetch><Store>Mailbox</Store><airsync:CollectionId>{EscapeXml(collectionId)}</airsync:CollectionId><airsync:ServerId>{EscapeXml(serverId)}</airsync:ServerId><Options><airsyncbase:BodyPreference><airsyncbase:Type>{typeValue}</airsyncbase:Type></airsyncbase:BodyPreference></Options></Fetch></ItemOperations>";
    }

    public static string BuildItemOperationsFetchAttachmentXml(string fileReference) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><ItemOperations xmlns=\"ItemOperations:\" xmlns:airsyncbase=\"AirSyncBase:\"><Fetch><Store>Mailbox</Store><airsyncbase:FileReference>{EscapeXml(fileReference)}</airsyncbase:FileReference></Fetch></ItemOperations>";

    public static string BuildSendMailRequestXml(string clientId, string mime) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><SendMail xmlns=\"ComposeMail:\"><ClientId>{EscapeXml(clientId)}</ClientId><SaveInSentItems/><MIME>{EscapeXml(mime)}</MIME></SendMail>";

    public static string BuildSmartReplyRequestXml(string collectionId, string serverId, string mime) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><SmartReply xmlns=\"ComposeMail:\"><Source><FolderId>{EscapeXml(collectionId)}</FolderId><ItemId>{EscapeXml(serverId)}</ItemId></Source><SaveInSentItems/><MIME>{EscapeXml(mime)}</MIME></SmartReply>";

    public static string BuildSmartForwardRequestXml(string collectionId, string serverId, string mime) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><SmartForward xmlns=\"ComposeMail:\"><Source><FolderId>{EscapeXml(collectionId)}</FolderId><ItemId>{EscapeXml(serverId)}</ItemId></Source><SaveInSentItems/><MIME>{EscapeXml(mime)}</MIME></SmartForward>";

    public static string BuildMeetingResponseRequestXml(string requestId, string collectionId, MeetingAction action) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><MeetingResponse xmlns=\"MeetingResponse:\"><Request><UserResponse>{action.ResponseType()}</UserResponse><CollectionId>{EscapeXml(collectionId)}</CollectionId><RequestId>{EscapeXml(requestId)}</RequestId></Request></MeetingResponse>";

    public static string BuildCalendarDeleteRequestXml(string syncKey, string collectionId, string serverId) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Sync xmlns=\"AirSync:\"><Collections><Collection><SyncKey>{EscapeXml(syncKey)}</SyncKey><CollectionId>{EscapeXml(collectionId)}</CollectionId><Commands><Delete><ServerId>{EscapeXml(serverId)}</ServerId></Delete></Commands></Collection></Collections></Sync>";

    public static List<NormalizedCalendarEvent> NormalizeInboxMeetingRequests(IEnumerable<ParsedInboxMeetingRequest> requests) =>
        requests.Where(r => r.Read != "1").Select(request =>
        {
            var organizer = ParseEmailContact(request.From);
            return new NormalizedCalendarEvent
            {
                ServerId = $"inbox:{request.ServerId}",
                Uid = string.IsNullOrEmpty(request.GlobalObjId) ? request.ServerId : request.GlobalObjId,
                Title = request.Subject,
                Description = request.BodyData,
                Location = request.Location,
                StartAt = NormalizeActiveSyncDateTime(request.StartTime),
                EndAt = NormalizeActiveSyncDateTime(string.IsNullOrEmpty(request.EndTime) ? request.StartTime : request.EndTime),
                AllDay = request.AllDayEvent == "1",
                Recurrence = null,
                Exceptions = [],
                Attendees = [],
                Organizer = organizer,
                ResponseStatus = MeetingResponseStatus.Pending,
                Source = EventSource.InboxInvitation,
                MeetingStatus = "3",
                IsCancelled = false,
                IsRecurring = false
            };
        }).ToList();

    public static List<NormalizedCalendarEvent> MergeCalendarEventsWithInvitations(
        List<NormalizedCalendarEvent> calendarEvents,
        List<NormalizedCalendarEvent> inboxInvitations)
    {
        var merged = calendarEvents.ToList();
        foreach (var invitation in inboxInvitations)
        {
            if (!merged.Any(existing => EventsMatch(existing, invitation)))
                merged.Add(invitation);
        }
        return SortCalendarEventsByStart(merged);
    }

    private static bool EventsMatch(NormalizedCalendarEvent lhs, NormalizedCalendarEvent rhs)
    {
        if (!string.IsNullOrEmpty(lhs.Uid) && lhs.Uid == rhs.Uid) return true;
        if (NormalizedEventTitle(lhs.Title) != NormalizedEventTitle(rhs.Title)) return false;
        var lhsStart = ActiveSyncDateParser.Parse(lhs.StartAt);
        var rhsStart = ActiveSyncDateParser.Parse(rhs.StartAt);
        if (lhsStart is null || rhsStart is null) return lhs.StartAt == rhs.StartAt;
        if (Math.Abs((lhsStart.Value - rhsStart.Value).TotalSeconds) > 120) return false;
        var lhsEnd = ActiveSyncDateParser.Parse(string.IsNullOrEmpty(lhs.EndAt) ? lhs.StartAt : lhs.EndAt);
        var rhsEnd = ActiveSyncDateParser.Parse(string.IsNullOrEmpty(rhs.EndAt) ? rhs.StartAt : rhs.EndAt);
        if (lhsEnd is null || rhsEnd is null) return true;
        return Math.Abs((lhsEnd.Value - rhsEnd.Value).TotalSeconds) <= 120;
    }

    private static string NormalizedEventTitle(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");

    public static MeetingResponseStatus MapResponseStatus(string responseType, string meetingStatus) => responseType switch
    {
        "1" => MeetingResponseStatus.Organizer,
        "2" => MeetingResponseStatus.Tentative,
        "3" => MeetingResponseStatus.Accepted,
        "4" => MeetingResponseStatus.Declined,
        "5" => MeetingResponseStatus.Pending,
        _ => meetingStatus == "1" ? MeetingResponseStatus.Organizer
            : meetingStatus == "3" ? MeetingResponseStatus.Pending
            : MeetingResponseStatus.Accepted
    };

    public static bool IsCancelledMeetingStatus(string meetingStatus) =>
        int.TryParse(meetingStatus, out var value) && (value & 4) == 4;

    public static CommandStatusResult ParseSyncCommandStatusXml(string xml)
    {
        var collectionBlock = GetAllTagBlocks(xml, "Collection").FirstOrDefault() ?? "";
        var responseBlocks = GetAllTagBlocks(collectionBlock, "Change").Concat(GetAllTagBlocks(collectionBlock, "Delete"));
        var itemStatuses = new Dictionary<string, string>();
        foreach (var block in responseBlocks)
        {
            var serverId = GetFirstTagText(block, "ServerId");
            if (!string.IsNullOrEmpty(serverId)) itemStatuses[serverId] = GetFirstTagText(block, "Status");
        }
        var status = GetFirstTagText(collectionBlock, "Status");
        if (string.IsNullOrEmpty(status)) status = GetFirstTagText(xml, "Status");
        return new CommandStatusResult { Status = status, SyncKey = GetFirstTagText(collectionBlock, "SyncKey"), ItemStatuses = itemStatuses };
    }

    public static CommandStatusResult ParseSimpleCommandStatusXml(string xml) =>
        new() { Status = GetFirstTagText(xml, "Status") };

    public static ItemOperationsFetchResult ParseItemOperationsFetchXml(string xml)
    {
        var fetchBlock = GetAllTagBlocks(xml, "Fetch").FirstOrDefault() ?? xml;
        var properties = GetAllTagBlocks(fetchBlock, "Properties").FirstOrDefault() ?? fetchBlock;
        var bodyBlock = GetAllTagBlocks(properties, "Body").FirstOrDefault() ?? "";
        var dataText = GetFirstTagText(properties, "Data");
        byte[]? decoded = null;
        try { decoded = Convert.FromBase64String(dataText); } catch { decoded = Encoding.UTF8.GetBytes(dataText); }
        var status = GetFirstTagText(fetchBlock, "Status");
        if (string.IsNullOrEmpty(status)) status = GetFirstTagText(xml, "Status");
        return new ItemOperationsFetchResult
        {
            Status = status,
            Body = ParseMailBody(bodyBlock),
            Attachments = ParseMailAttachments(properties),
            Data = decoded,
            FileName = GetFirstTagText(properties, "DisplayName"),
            ContentType = GetFirstTagText(properties, "ContentType")
        };
    }

    private static MailBody? ParseMailBody(string bodyBlock)
    {
        if (string.IsNullOrEmpty(bodyBlock)) return null;
        var bodyType = GetFirstTagText(bodyBlock, "Type") switch
        {
            "1" => MailBodyType.Plain,
            "2" => MailBodyType.Html,
            "4" => MailBodyType.Mime,
            _ => MailBodyType.Unknown
        };
        return new MailBody
        {
            Type = bodyType,
            Data = GetFirstTagText(bodyBlock, "Data"),
            IsTruncated = GetFirstTagText(bodyBlock, "Truncated") == "1"
        };
    }

    private static List<MailAttachment> ParseMailAttachments(string applicationData) =>
        GetAllTagBlocks(applicationData, "Attachment").Select(block =>
        {
            var displayName = GetFirstTagText(block, "DisplayName");
            var fileReference = GetFirstTagText(block, "FileReference");
            if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(fileReference)) return null;
            var contentId = GetFirstTagText(block, "ContentId");
            return new MailAttachment
            {
                DisplayName = string.IsNullOrEmpty(displayName) ? "Вложение" : displayName,
                FileReference = fileReference,
                EstimatedSize = int.TryParse(GetFirstTagText(block, "EstimatedDataSize"), out var size) ? size : null,
                ContentType = string.IsNullOrEmpty(GetFirstTagText(block, "ContentType")) ? null : GetFirstTagText(block, "ContentType"),
                IsInline = GetFirstTagText(block, "IsInline") == "1",
                ContentId = string.IsNullOrEmpty(contentId) ? null : contentId
            };
        }).OfType<MailAttachment>().ToList();

    private static CalendarOrganizer? ParseEmailContact(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        var match = Regex.Match(trimmed, "\"([^\"]+)\"\\s*<([^>]+)>");
        if (match.Success) return new CalendarOrganizer { Name = match.Groups[1].Value, Email = match.Groups[2].Value };
        if (trimmed.Contains('@')) return new CalendarOrganizer { Name = trimmed, Email = trimmed };
        return new CalendarOrganizer { Name = trimmed, Email = "" };
    }

    private static List<MailAddress> ParseMailAddressList(string raw) =>
        raw.Split(';').Select(ParseMailAddress).OfType<MailAddress>().ToList();

    private static MailAddress? ParseMailAddress(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        var match = Regex.Match(trimmed, "\"?([^\"<]*)\"?\\s*<([^>]+)>");
        if (match.Success)
            return new MailAddress { Name = match.Groups[1].Value.Trim(), Email = match.Groups[2].Value.Trim() };
        if (trimmed.Contains('@')) return new MailAddress { Name = "", Email = trimmed };
        return new MailAddress { Name = trimmed, Email = "" };
    }

    public static List<NormalizedCalendarEvent> SortCalendarEventsByStart(List<NormalizedCalendarEvent> events) =>
        events.OrderBy(e => ActiveSyncDateParser.Parse(e.StartAt) ?? DateTime.MinValue).ToList();

    private const string PolicyType = "MS-EAS-Provisioning-WBXML";

    public static string BuildInitialProvisionRequestXml(ProvisionRequestConfig config) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Provision xmlns=\"Provision:\" xmlns:settings=\"Settings:\"><settings:DeviceInformation><settings:Set><settings:Model>{EscapeXml(config.DeviceModel)}</settings:Model><settings:IMEI>{EscapeXml(config.DeviceImei)}</settings:IMEI><settings:FriendlyName>{EscapeXml(config.DeviceFriendlyName)}</settings:FriendlyName><settings:OS>{EscapeXml(config.DeviceOs)}</settings:OS><settings:OSLanguage>{EscapeXml(config.DeviceOsLanguage)}</settings:OSLanguage><settings:PhoneNumber>{EscapeXml(config.DevicePhoneNumber)}</settings:PhoneNumber><settings:MobileOperator>{EscapeXml(config.DeviceMobileOperator)}</settings:MobileOperator><settings:UserAgent>{EscapeXml(config.UserAgent)}</settings:UserAgent></settings:Set></settings:DeviceInformation><Policies><Policy><PolicyType>{PolicyType}</PolicyType></Policy></Policies></Provision>";

    public static string BuildProvisionAckRequestXml(string policyKey, string status = "1") =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><Provision xmlns=\"Provision:\"><Policies><Policy><PolicyType>{PolicyType}</PolicyType><PolicyKey>{EscapeXml(policyKey)}</PolicyKey><Status>{EscapeXml(status)}</Status></Policy></Policies></Provision>";

    public static ProvisionResponse ParseProvisionResponseXml(string xml)
    {
        var match = Regex.Match(xml, "<Policy(?:\\s[^>]*)?>([\\s\\S]*?)</Policy>", RegexOptions.IgnoreCase);
        var policyBlock = match.Success ? match.Groups[1].Value : "";
        return new ProvisionResponse
        {
            Status = GetFirstTagText(xml, "Status"),
            PolicyType = GetFirstTagText(policyBlock, "PolicyType"),
            PolicyStatus = GetFirstTagText(policyBlock, "Status"),
            PolicyKey = GetFirstTagText(policyBlock, "PolicyKey"),
            RemoteWipe = xml.Contains("<RemoteWipe")
        };
    }

    private static bool UsesLegacyClass(string protocolVersion) =>
        protocolVersion is "2.5" or "12.0" or "12.1";

    public static string NormalizeActiveSyncDateTime(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (Regex.IsMatch(value, @"^\d{8}T\d{6}Z?$"))
        {
            var year = value[..4];
            var month = value.Substring(4, 2);
            var day = value.Substring(6, 2);
            var hour = value.Substring(9, 2);
            var minute = value.Substring(11, 2);
            var second = value.Substring(13, 2);
            return $"{year}-{month}-{day}T{hour}:{minute}:{second}.000Z";
        }
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
            return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        return "";
    }

    private static int ParseInteger(string value, int fallback) => int.TryParse(value, out var i) ? i : fallback;
    private static int? ParseIntegerOptional(string value) => string.IsNullOrEmpty(value) ? null : int.TryParse(value, out var i) ? i : null;

    private static string? DecodeActiveSyncTimeZone(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            var data = Convert.FromBase64String(value);
            var decoded = Encoding.Unicode.GetString(data);
            var cleaned = new string(decoded.Where(c => c > 31).ToArray()).Trim();
            return string.IsNullOrEmpty(cleaned) ? null : cleaned;
        }
        catch { return null; }
    }

    private static string NormalizeAttendeeType(string type) => type switch
    {
        "2" => "optional",
        "3" => "resource",
        _ => "required"
    };
}
