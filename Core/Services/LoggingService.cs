using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CloudflaredTunnelHost.Core.Models;

namespace CloudflaredTunnelHost.Core.Services;

public sealed partial class LoggingService(AppPaths paths)
{
    private readonly ConcurrentQueue<LogEntry> _pending = new();
    private readonly object _fileLock = new();
    public bool SaveToFile { get; set; }
    public string? FileError { get; private set; }
    public int PendingCount => _pending.Count;

    [GeneratedRegex(@"(?i)(?:https?://)?[a-z0-9-]+\.trycloudflare\.com")]
    private static partial Regex PublicUrls();
    [GeneratedRegex(@"(?i)[\w.+-]+@[\w.-]+\.[a-z]{2,}")]
    private static partial Regex Emails();
    [GeneratedRegex("(?i)\\b(access_token|token|secret|authorization)\\b([=:\\s\"]+)(?:(?:Bearer|Basic)\\s+)?[^\\s,\"]+")]
    private static partial Regex Secrets();
    public static string Redact(string text) => Secrets().Replace(Emails().Replace(PublicUrls().Replace(text, "[public-url]"), "[email]"), "$1$2[redacted]");

    public void Write(string message, string level = "INFO")
    {
        if (message.Length > 8192) message = message[..8192] + " [truncated]";
        var entry = new LogEntry(DateTimeOffset.Now, level.ToUpperInvariant(), Redact(message));
        _pending.Enqueue(entry);
        while (_pending.Count > 5000) _pending.TryDequeue(out _);
        if (!SaveToFile) return;
        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(paths.Logs);
                var file = Path.Combine(paths.Logs, "cloudflared.log");
                if (File.Exists(file) && new FileInfo(file).Length > 5 * 1024 * 1024) File.Move(file, file + ".1", true);
                File.AppendAllText(file, entry + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { FileError = ex.Message; SaveToFile = false; }
        }
    }

    public IReadOnlyList<LogEntry> Drain(int limit = 200)
    {
        var result = new List<LogEntry>();
        while (result.Count < limit && _pending.TryDequeue(out var item)) result.Add(item);
        return result;
    }
    public void Clear() { while (_pending.TryDequeue(out _)) { } }
}
