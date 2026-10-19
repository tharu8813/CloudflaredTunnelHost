namespace CloudflaredTunnelHost.Core.Models;

public enum TunnelState { Idle, Validating, Starting, Connecting, Connected, Reconnecting, Stopping, Error }

public sealed record TunnelSnapshot(TunnelState State, string Message, string? PublicUrl = null,
    DateTimeOffset? ConnectedAt = null, int Retry = 0, string Reachability = "아직 확인하지 않았습니다.", bool EdgeReady = false, bool LocalReady = false)
{
    public bool IsActive => State is not (TunnelState.Idle or TunnelState.Error);
}

public sealed record DiagnosticResult(bool Success, string Message, string? Details = null);
public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message)
{
    public string Time => Timestamp.ToLocalTime().ToString("HH:mm:ss");
    public override string ToString() => $"{Timestamp:O} [{Level}] {Message}";
}
