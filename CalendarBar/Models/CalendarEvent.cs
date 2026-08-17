using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CalendarBar;

public enum MeetingResponseStatus
{
    Organizer,
    Accepted,
    Tentative,
    Declined,
    Pending
}

public static class MeetingResponseStatusText
{
    public static string DisplayName(this MeetingResponseStatus status) => status switch
    {
        MeetingResponseStatus.Organizer => "Организатор",
        MeetingResponseStatus.Accepted => "Принято",
        MeetingResponseStatus.Tentative => "Под вопросом",
        MeetingResponseStatus.Declined => "Отклонено",
        MeetingResponseStatus.Pending => "Ожидает ответа",
        _ => ""
    };

    public static bool IsHighlighted(this MeetingResponseStatus status) =>
        status is MeetingResponseStatus.Pending or MeetingResponseStatus.Tentative;
}

public sealed class EventAttendee
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "required";

    [JsonIgnore]
    public string Id => string.IsNullOrEmpty(Email) ? Name : Email;

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrEmpty(Name) ? Name : !string.IsNullOrEmpty(Email) ? Email : "Участник";

    [JsonIgnore]
    public string RoleLabel => Role switch
    {
        "optional" => "Необязательный",
        "resource" => "Ресурс",
        _ => "Обязательный"
    };
}

public enum EventSource
{
    Calendar,
    InboxInvitation
}

public sealed class CalendarEvent
{
    public string Id { get; init; } = "";
    public string Subject { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Location { get; set; }
    public string? Body { get; set; }
    public string? Organizer { get; set; }
    public List<EventAttendee> Attendees { get; set; } = [];
    public bool IsAllDay { get; set; }
    public int? ReminderMinutes { get; set; }
    public MeetingResponseStatus ResponseStatus { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsRecurring { get; set; }
    public string? SourceTimeZone { get; set; }
    public EventSource Source { get; set; }

    private static readonly CultureInfo Ru = new("ru-RU");

    [JsonIgnore]
    public string DurationText
    {
        get
        {
            if (IsAllDay) return "Весь день";
            return $"{StartDate.ToString("HH:mm", Ru)} – {EndDate.ToString("HH:mm", Ru)}";
        }
    }

    [JsonIgnore]
    public bool IsUpcoming => !IsCancelled && EndDate > DateTime.Now;

    [JsonIgnore]
    public bool IsFuture => StartDate > DateTime.Now;

    public bool Occurs(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        return StartDate < dayEnd && EndDate > dayStart;
    }
}

public enum MeetingAction
{
    Accept,
    Tentative,
    Decline
}

public static class MeetingActionExt
{
    public static string ResponseType(this MeetingAction action) => action switch
    {
        MeetingAction.Accept => "1",
        MeetingAction.Tentative => "2",
        MeetingAction.Decline => "3",
        _ => "1"
    };
}
