using System.Text.Json;

namespace CalendarBar;

public sealed class ActiveSyncSyncKeys
{
    public string Calendar { get; set; } = "0";
    public string Inbox { get; set; } = "0";
}

public sealed class ActiveSyncSyncKeyStore
{
    public static ActiveSyncSyncKeyStore Shared { get; } = new();

    private const string Prefix = "activeSyncSyncKeys.";
    private readonly Dictionary<string, Dictionary<string, string>> _mailFolderKeys = [];
    private readonly object _mailFolderLock = new();

    public ActiveSyncSyncKeys Load(string accountKey)
    {
        if (string.IsNullOrEmpty(accountKey)) return new ActiveSyncSyncKeys();
        var data = AppData.GetString(Prefix + accountKey);
        if (string.IsNullOrEmpty(data)) return new ActiveSyncSyncKeys();
        try { return JsonSerializer.Deserialize<ActiveSyncSyncKeys>(data) ?? new ActiveSyncSyncKeys(); }
        catch { return new ActiveSyncSyncKeys(); }
    }

    public void Save(ActiveSyncSyncKeys keys, string accountKey)
    {
        if (string.IsNullOrEmpty(accountKey)) return;
        AppData.SetString(Prefix + accountKey, JsonSerializer.Serialize(keys));
    }

    public void UpdateCalendar(string syncKey, string accountKey)
    {
        var keys = Load(accountKey);
        keys.Calendar = syncKey;
        Save(keys, accountKey);
    }

    public void UpdateInbox(string syncKey, string accountKey)
    {
        var keys = Load(accountKey);
        keys.Inbox = syncKey;
        Save(keys, accountKey);
    }

    public Dictionary<string, string> LoadMailFolderKeys(string accountKey)
    {
        if (string.IsNullOrEmpty(accountKey)) return [];
        lock (_mailFolderLock)
            return _mailFolderKeys.TryGetValue(accountKey, out var map) ? new Dictionary<string, string>(map) : [];
    }

    public string MailFolderSyncKey(string accountKey, string collectionId)
    {
        var keys = LoadMailFolderKeys(accountKey);
        return keys.TryGetValue(collectionId, out var key) ? key : "0";
    }

    public void UpdateMailFolder(string syncKey, string accountKey, string collectionId)
    {
        if (string.IsNullOrEmpty(accountKey) || string.IsNullOrEmpty(collectionId)) return;
        lock (_mailFolderLock)
        {
            if (!_mailFolderKeys.TryGetValue(accountKey, out var map))
            {
                map = [];
                _mailFolderKeys[accountKey] = map;
            }
            map[collectionId] = syncKey;
        }
    }

    public void Reset(string accountKey)
    {
        if (string.IsNullOrEmpty(accountKey)) return;
        AppData.Remove(Prefix + accountKey);
        AppData.Remove("activeSyncMailFolderSyncKeys." + accountKey);
        lock (_mailFolderLock) _mailFolderKeys.Remove(accountKey);
    }
}

public static class ActiveSyncSyncStatus
{
    public static bool RequiresFullResync(string status) => status is "3" or "132" or "6";
    public static bool IsTransient(string status) => status is "5" or "16";
    public static bool RequiresFolderResync(string status) => status == "12";

    public static string UserMessage(string status) => status switch
    {
        "3" or "132" => "Сессия синхронизации устарела, выполняется повторная загрузка…",
        "5" or "16" => "Временная ошибка сервера, повторяем запрос…",
        "6" => "Пропущено повреждённое событие, выполняется повторная загрузка…",
        "12" => "Структура папок изменилась, обновляем календарь…",
        "4" => "Ошибка протокола ActiveSync: сервер отклонил параметры синхронизации.",
        "108" => "Некорректный DeviceId.",
        "142" or "144" => "Требуется повторная регистрация устройства.",
        _ => $"ActiveSync status {status}"
    };
}
