using System.Net.Mail;
using System.Text.Json.Serialization;

namespace CloudflaredTunnelHost.Core.Models;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public int Port { get; init; } = 8000;
    public string ServiceType { get; init; } = "HTTP";
    public string LogLevel { get; init; } = "info";
    public string Protocol { get; init; } = "auto";
    public bool NoTlsVerify { get; init; }
    public bool SaveLogFile { get; init; }
    public bool AutoReconnect { get; init; } = true;
    public int ReconnectLimit { get; init; } = 3;
    public bool StartTunnelOnLaunch { get; init; }
    public bool StartInTray { get; init; }
    public bool MinimizeToTray { get; init; } = true;
    public bool CloseToTray { get; init; }
    public bool Notifications { get; init; } = true;
    public string Theme { get; init; } = "Dark";
    public string AllowedEmails { get; init; } = "";
    [JsonIgnore] public string LocalUrl => $"{ServiceType.ToLowerInvariant()}://localhost:{Port}";

    [JsonIgnore] public string[] EmailList => AllowedEmails.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Validate()
    {
        if (SchemaVersion != 1) throw new ArgumentException("이 버전에서 지원하지 않는 설정 파일입니다.");
        if (Port is < 1 or > 65535) throw new ArgumentException("포트는 1부터 65535 사이의 숫자로 입력하세요.");
        if (ServiceType is not ("HTTP" or "HTTPS")) throw new ArgumentException("서비스 유형은 HTTP 또는 HTTPS여야 합니다.");
        if (LogLevel is not ("debug" or "info" or "warn" or "error" or "fatal")) throw new ArgumentException("로그 레벨을 다시 선택하세요.");
        if (Protocol is not ("auto" or "quic" or "http2")) throw new ArgumentException("연결 프로토콜을 다시 선택하세요.");
        if (ReconnectLimit is < 0 or > 10) throw new ArgumentException("재연결 횟수는 0부터 10 사이여야 합니다.");
        if (Theme is not ("Dark" or "Light")) throw new ArgumentException("지원하지 않는 테마입니다.");
        if (AllowedEmails is null || AllowedEmails.Length > 4096) throw new ArgumentException("이메일 목록이 올바르지 않거나 너무 깁니다.");
        foreach (var email in EmailList)
            if (!MailAddress.TryCreate(email, out var address) || address.Address != email || email.Contains('*'))
                throw new ArgumentException("접근 허용 목록에는 정확한 이메일 주소를 쉼표로 구분해 입력하세요.");
    }
}
