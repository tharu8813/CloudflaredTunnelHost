using System.Text.Json;
using CloudflaredTunnelHost.Core.Models;

namespace CloudflaredTunnelHost.Core.Services;

public sealed class SettingsService(AppPaths paths)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string? Notice { get; private set; }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(paths.Settings))
            {
                if (new FileInfo(paths.Settings).Length > 128 * 1024) throw new ArgumentException("설정 파일이 너무 큽니다.");
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(paths.Settings)) ?? throw new JsonException();
                settings.Validate();
                return settings;
            }
            if (!File.Exists(paths.LegacySettings)) return new();
            if (new FileInfo(paths.LegacySettings).Length > 128 * 1024) throw new ArgumentException("기존 설정 파일이 너무 큽니다.");
            var values = File.ReadLines(paths.LegacySettings).Select(x => x.Split('=', 2)).Where(x => x.Length == 2)
                .GroupBy(x => x[0].Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last()[1].Trim(), StringComparer.OrdinalIgnoreCase);
            var migrated = new AppSettings
            {
                Port = int.TryParse(values.GetValueOrDefault("Port"), out var port) && port is > 0 and <= 65535 ? port : 8000,
                ServiceType = values.GetValueOrDefault("ServiceType", "HTTP").ToUpperInvariant() == "HTTPS" ? "HTTPS" : "HTTP",
                LogLevel = Normalize(values.GetValueOrDefault("LogLevel"), ["debug", "info", "warn", "error", "fatal"], "info"),
                Protocol = Normalize(values.GetValueOrDefault("Protocol"), ["auto", "quic", "http2"], "auto"),
                NoTlsVerify = bool.TryParse(values.GetValueOrDefault("HttpsNoVerify"), out var noVerify) && noVerify,
                SaveLogFile = bool.TryParse(values.GetValueOrDefault("LogFile"), out var logFile) && logFile
            };
            Save(migrated);
            Notice = "기존 설정을 JSON으로 이전했습니다. 원본 settings.txt는 보존했습니다.";
            return migrated;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Notice = "설정을 읽을 수 없어 기본값을 사용합니다. 원본 파일은 보존됩니다. " + ex.Message;
            return new();
        }
    }

    private static string Normalize(string? value, string[] allowed, string fallback) =>
        allowed.Contains(value?.ToLowerInvariant()) ? value!.ToLowerInvariant() : fallback;

    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(paths.Root);
        var temp = paths.Settings + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            if (File.Exists(paths.Settings)) File.Replace(temp, paths.Settings, paths.Settings + ".bak");
            else File.Move(temp, paths.Settings);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
