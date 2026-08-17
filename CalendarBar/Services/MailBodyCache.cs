using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CalendarBar;

public sealed class MailBodyCache
{
    public static MailBodyCache Shared { get; } = new();

    private static readonly TimeSpan MaxEntryAge = TimeSpan.FromDays(14);
    private const long MaxTotalBytes = 64 * 1024 * 1024;

    private readonly string? _root;
    private bool _didPrune;

    private MailBodyCache()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalendarBar", "MailCache");
    }

    public MailBody? Body(string accountKey, string messageId)
    {
        PruneIfNeeded();
        var url = FilePath(accountKey, "bodies", messageId);
        if (url is null || !File.Exists(url)) return null;
        MarkUsed(url);
        try { return JsonSerializer.Deserialize<MailBody>(File.ReadAllText(url)); }
        catch { return null; }
    }

    public void StoreBody(MailBody body, string accountKey, string messageId)
    {
        var url = FilePath(accountKey, "bodies", messageId);
        if (url is null) return;
        Write(JsonSerializer.SerializeToUtf8Bytes(body), url);
    }

    public byte[]? Attachment(string accountKey, string fileReference)
    {
        PruneIfNeeded();
        var url = FilePath(accountKey, "attachments", fileReference);
        if (url is null || !File.Exists(url)) return null;
        MarkUsed(url);
        try { return File.ReadAllBytes(url); }
        catch { return null; }
    }

    public void StoreAttachment(byte[] data, string accountKey, string fileReference)
    {
        var url = FilePath(accountKey, "attachments", fileReference);
        if (url is null) return;
        Write(data, url);
    }

    public Task Clear(string accountKey)
    {
        var root = AccountRoot(accountKey);
        if (root is not null && Directory.Exists(root))
            try { Directory.Delete(root, true); } catch { }
        return Task.CompletedTask;
    }

    private string? AccountRoot(string accountKey)
    {
        if (_root is null) return null;
        var normalized = accountKey.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized)) return null;
        return Path.Combine(_root, Digest(normalized));
    }

    private string? FilePath(string accountKey, string directory, string key)
    {
        var accountRoot = AccountRoot(accountKey);
        if (accountRoot is null || string.IsNullOrEmpty(key)) return null;
        return Path.Combine(accountRoot, directory, Digest(key));
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void Write(byte[] data, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    private static void MarkUsed(string path)
    {
        try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); } catch { }
    }

    private void PruneIfNeeded()
    {
        if (_didPrune || _root is null || !Directory.Exists(_root)) { _didPrune = true; return; }
        _didPrune = true;
        var cutoff = DateTime.UtcNow - MaxEntryAge;
        var entries = new List<(string Path, DateTime Date, long Size)>();
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff) { info.Delete(); continue; }
                entries.Add((file, info.LastWriteTimeUtc, info.Length));
            }
            catch { }
        }
        var total = entries.Sum(e => e.Size);
        if (total <= MaxTotalBytes) return;
        foreach (var entry in entries.OrderBy(e => e.Date))
        {
            try { File.Delete(entry.Path); } catch { }
            total -= entry.Size;
            if (total <= MaxTotalBytes) break;
        }
    }
}
