using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CalendarBar;

public sealed class MailAddress
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";

    [JsonIgnore]
    public string Id => string.IsNullOrEmpty(Email) ? Name : Email.ToLowerInvariant();

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrEmpty(Name) ? Name : !string.IsNullOrEmpty(Email) ? Email : "Без отправителя";
}

public sealed class MailAttachment
{
    public string DisplayName { get; init; } = "";
    public string FileReference { get; init; } = "";
    public int? EstimatedSize { get; init; }
    public string? ContentType { get; init; }
    public bool IsInline { get; init; }
    public string? ContentId { get; set; }

    [JsonIgnore]
    public string Id => string.IsNullOrEmpty(FileReference) ? DisplayName : FileReference;

    [JsonIgnore]
    public string SizeText
    {
        get
        {
            if (EstimatedSize is not > 0) return "";
            var bytes = (double)EstimatedSize.Value;
            if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024):0.#} МБ";
            if (bytes >= 1024) return $"{bytes / 1024:0.#} КБ";
            return $"{bytes:0} Б";
        }
    }
}

public enum MailBodyType { Plain, Html, Mime, Unknown }

public sealed class MailBody
{
    public MailBodyType Type { get; set; }
    public string Data { get; set; } = "";
    public bool IsTruncated { get; set; }
}

public sealed class MailMessage
{
    public string ServerId { get; init; } = "";
    public string CollectionId { get; init; } = "";
    public string Subject { get; set; } = "";
    public MailAddress? From { get; set; }
    public List<MailAddress> To { get; set; } = [];
    public List<MailAddress> Cc { get; set; } = [];
    public List<MailAddress> ReplyTo { get; set; } = [];
    public DateTime? DateReceived { get; set; }
    public bool IsRead { get; set; }
    public string? Importance { get; set; }
    public string MessageClass { get; set; } = "";
    public string ConversationId { get; set; } = "";
    public string ConversationIndex { get; set; } = "";
    public string ThreadTopic { get; set; } = "";
    public MailBody? Body { get; set; }
    public string? Preview { get; set; }
    public List<MailAttachment> Attachments { get; set; } = [];

    [JsonIgnore]
    public string Id => ServerId;

    [JsonIgnore]
    public string DisplaySubject
    {
        get
        {
            var s = Subject.Trim();
            return string.IsNullOrEmpty(s) ? "(без темы)" : Subject;
        }
    }

    [JsonIgnore]
    public string ThreadKey
    {
        get
        {
            if (!string.IsNullOrEmpty(ConversationId)) return ConversationId;
            if (!string.IsNullOrEmpty(ConversationIndex)) return ConversationIndex;
            var normalized = Regex.Replace(DisplaySubject.ToLowerInvariant(), @"^(re|fw|fwd):\s*", "");
            var from = From?.Email.ToLowerInvariant() ?? From?.Name.ToLowerInvariant() ?? "";
            return $"{normalized}|{from}";
        }
    }

    [JsonIgnore]
    public string DisplayBodyText =>
        Body is null ? Preview ?? "" : TextContentFormatter.PlainText(Body.Data);

    [JsonIgnore]
    public string PreviewText
    {
        get
        {
            if (!string.IsNullOrEmpty(Preview)) return Preview;
            if (Body is null) return "";
            return Body.Type == MailBodyType.Html
                ? TextContentFormatter.LightweightPlainText(Body.Data)
                : Body.Data.Trim();
        }
    }

    [JsonIgnore]
    public string ReceivedText
    {
        get
        {
            if (DateReceived is null) return "";
            var locale = new CultureInfo("ru-RU");
            return DateReceived.Value.Date == DateTime.Today
                ? DateReceived.Value.ToString("HH:mm", locale)
                : DateReceived.Value.ToString("d MMM, HH:mm", locale);
        }
    }
}

public enum MailFolderKind { Inbox, Sent, Drafts, Trash }

public static class MailFolderKindText
{
    public static string Title(this MailFolderKind kind) => kind switch
    {
        MailFolderKind.Inbox => "Входящие",
        MailFolderKind.Sent => "Отправленные",
        MailFolderKind.Drafts => "Черновики",
        MailFolderKind.Trash => "Корзина",
        _ => kind.ToString()
    };

    public static IReadOnlyList<MailFolderKind> All { get; } =
        [MailFolderKind.Inbox, MailFolderKind.Sent, MailFolderKind.Drafts, MailFolderKind.Trash];
}

public sealed class MailThread
{
    public string Id { get; init; } = "";
    public List<MailMessage> Messages { get; set; } = [];

    public static List<MailThread> Grouped(IEnumerable<MailMessage> messages)
    {
        return messages
            .GroupBy(m => m.ThreadKey)
            .Select(g => new MailThread
            {
                Id = g.Key,
                Messages = g.OrderByDescending(IsNewerKey).ThenByDescending(m => m.ServerId).ToList()
            })
            .OrderByDescending(t => t.LatestMessage?.DateReceived ?? DateTime.MinValue)
            .ThenByDescending(t => t.LatestMessage?.ServerId ?? "")
            .ToList();
    }

    private static DateTime IsNewerKey(MailMessage m) => m.DateReceived ?? DateTime.MinValue;

    [JsonIgnore]
    public MailMessage? LatestMessage =>
        Messages.OrderByDescending(m => m.DateReceived ?? DateTime.MinValue)
            .ThenByDescending(m => m.ServerId)
            .FirstOrDefault();

    [JsonIgnore]
    public int UnreadCount => Messages.Count(m => !m.IsRead);

    [JsonIgnore]
    public string Subject => LatestMessage?.DisplaySubject ?? "(без темы)";
}

public sealed class MailSyncSnapshot
{
    public List<MailMessage> Messages { get; init; } = [];
    public List<string> DeletedServerIds { get; init; } = [];
    public List<ParsedInboxMeetingRequest> MeetingRequests { get; init; } = [];
}

public sealed class MailSyncRequest
{
    public MailSyncScope Scope { get; init; } = MailSyncScope.AllFolders;
    public bool NotifyNew { get; init; }

    public MailSyncRequest Merged(MailSyncRequest other) => new()
    {
        Scope = Scope.Merged(other.Scope),
        NotifyNew = NotifyNew || other.NotifyNew
    };
}

public abstract record MailSyncScope
{
    public sealed record AllFoldersScope : MailSyncScope;
    public sealed record FolderScope(MailFolderKind Kind) : MailSyncScope;

    public static MailSyncScope AllFolders { get; } = new AllFoldersScope();
    public static MailSyncScope ForFolder(MailFolderKind kind) => new FolderScope(kind);

    public MailSyncScope Merged(MailSyncScope other) => (this, other) switch
    {
        (AllFoldersScope, _) or (_, AllFoldersScope) => AllFolders,
        (FolderScope lhs, FolderScope rhs) => lhs.Kind == rhs.Kind ? lhs : AllFolders,
        _ => AllFolders
    };

    public List<MailFolderKind> Folders(MailFolderKind preferred)
    {
        if (this is FolderScope folder) return [folder.Kind];
        var folders = MailFolderKindText.All.Where(f => f != preferred).ToList();
        folders.Insert(0, preferred);
        return folders;
    }
}
