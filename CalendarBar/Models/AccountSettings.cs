using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CalendarBar;

public sealed class AccountSettings
{
    public string Email { get; set; } = "";
    public string Server { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Username { get; set; } = "";
    public string DeviceId { get; set; } = "";

    [JsonIgnore]
    public static AccountSettings Empty => new();

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Server) && !string.IsNullOrEmpty(Username);

    [JsonIgnore]
    public string ActiveSyncAuthUsername
    {
        get
        {
            if (!string.IsNullOrEmpty(Domain)) return $"{Domain}\\{Username}";
            if (!string.IsNullOrEmpty(Email)) return Email;
            return Username;
        }
    }

    [JsonIgnore]
    public string ActiveSyncUserParam => Email;

    [JsonIgnore]
    public string ActiveSyncEndpoint => "";

    [JsonIgnore]
    public Uri ActiveSyncUrl => new($"https://{Server}/Microsoft-Server-ActiveSync");

    public static string GenerateDeviceId()
    {
        var hex = Guid.NewGuid().ToString("N").ToUpperInvariant();
        return "Appl" + hex[..Math.Min(28, hex.Length)];
    }

    [JsonIgnore]
    public string ResolvedDeviceId
    {
        get
        {
            var candidate = DeviceId.Trim();
            return IsValidDeviceId(candidate) ? candidate : GenerateDeviceId();
        }
    }

    public static bool IsValidDeviceId(string value) =>
        value.Length is >= 8 and <= 64 && Regex.IsMatch(value, "^[A-Za-z0-9]+$");
}

public abstract record SyncState
{
    public sealed record Idle : SyncState;
    public sealed record Syncing : SyncState;
    public sealed record Success(DateTime Date) : SyncState;
    public sealed record Failure(string Message) : SyncState;

    public string StatusText => this switch
    {
        Idle => "Ожидание",
        Syncing => "Синхронизация…",
        Success(var date) => FormatSuccess(date),
        Failure(var message) => message,
        _ => ""
    };

    public bool IsError => this is Failure;

    private static string FormatSuccess(DateTime date)
    {
        var locale = new System.Globalization.CultureInfo("ru-RU");
        if (date.Date == DateTime.Today)
            return date.ToString("'Последняя синхронизация в' HH:mm", locale);
        return date.ToString("'Последняя синхронизация' d MMM 'в' HH:mm", locale);
    }
}
