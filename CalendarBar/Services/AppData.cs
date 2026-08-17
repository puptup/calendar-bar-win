using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CalendarBar;

/// Simple JSON key-value store in %AppData%\CalendarBar\settings.json
public static class AppData
{
    private static readonly object Gate = new();
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CalendarBar", "settings.json");

    private static Dictionary<string, JsonElement> _cache = Load();

    private static Dictionary<string, JsonElement> Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var json = File.ReadAllText(Path);
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    private static void Persist()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(_cache));
    }

    public static string? GetString(string key)
    {
        lock (Gate) return _cache.TryGetValue(key, out var v) ? v.GetString() : null;
    }

    public static bool GetBool(string key, bool fallback = false)
    {
        lock (Gate)
        {
            if (!_cache.TryGetValue(key, out var v)) return fallback;
            return v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.False ? false : fallback);
        }
    }

    public static bool HasKey(string key)
    {
        lock (Gate) return _cache.ContainsKey(key);
    }

    public static int GetInt(string key, int fallback = 0)
    {
        lock (Gate)
        {
            if (!_cache.TryGetValue(key, out var v)) return fallback;
            return v.TryGetInt32(out var i) ? i : fallback;
        }
    }

    public static double GetDouble(string key, double fallback = 0)
    {
        lock (Gate)
        {
            if (!_cache.TryGetValue(key, out var v)) return fallback;
            return v.TryGetDouble(out var d) ? d : fallback;
        }
    }

    public static byte[]? GetBytes(string key)
    {
        lock (Gate)
        {
            if (!_cache.TryGetValue(key, out var v)) return null;
            return v.ValueKind == JsonValueKind.String ? Convert.FromBase64String(v.GetString() ?? "") : null;
        }
    }

    public static string[] GetStringArray(string key)
    {
        lock (Gate)
        {
            if (!_cache.TryGetValue(key, out var v) || v.ValueKind != JsonValueKind.Array) return [];
            return v.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
        }
    }

    public static void SetString(string key, string value) => Set(key, JsonSerializer.SerializeToElement(value));
    public static void SetBool(string key, bool value) => Set(key, JsonSerializer.SerializeToElement(value));
    public static void SetInt(string key, int value) => Set(key, JsonSerializer.SerializeToElement(value));
    public static void SetDouble(string key, double value) => Set(key, JsonSerializer.SerializeToElement(value));
    public static void SetBytes(string key, byte[] value) => Set(key, JsonSerializer.SerializeToElement(Convert.ToBase64String(value)));
    public static void SetStringArray(string key, IEnumerable<string> value) => Set(key, JsonSerializer.SerializeToElement(value.ToArray()));

    public static void Remove(string key)
    {
        lock (Gate)
        {
            if (_cache.Remove(key)) Persist();
        }
    }

    private static void Set(string key, JsonElement value)
    {
        lock (Gate)
        {
            _cache[key] = value;
            Persist();
        }
    }
}
