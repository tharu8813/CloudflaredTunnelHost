using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text.Json;
using CloudflaredTunnelHost.Core.Models;

namespace CloudflaredTunnelHost.Core.Services;

public sealed partial class CloudflaredService : IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly LoggingService _logs;
    private readonly ITunnelProcessManager _process;
    private readonly Func<AppSettings, CancellationToken, Task<DiagnosticResult>> _diagnose;
    private readonly Func<int, CancellationToken, Task<bool>> _ready;
    private readonly Func<string, CancellationToken, Task<DiagnosticResult>> _probe;
    private readonly Func<string, string[], CancellationToken, Task<string>> _inspect;
    private readonly object _gate = new();
    private CancellationTokenSource? _session;
    private Task _run = Task.CompletedTask;
    private TunnelSnapshot _snapshot = new(TunnelState.Idle, "로컬 서버를 인터넷에 연결할 준비가 되었습니다.");
    public TunnelSnapshot Snapshot { get { lock (_gate) return _snapshot; } }
    public event Action<TunnelSnapshot>? Changed;

    public CloudflaredService(AppPaths paths, LoggingService logs, ITunnelProcessManager? process = null,
        Func<AppSettings, CancellationToken, Task<DiagnosticResult>>? diagnose = null,
        Func<int, CancellationToken, Task<bool>>? ready = null,
        Func<string, CancellationToken, Task<DiagnosticResult>>? probe = null,
        Func<string, string[], CancellationToken, Task<string>>? inspect = null)
    {
        _paths = paths; _logs = logs; _process = process ?? new TunnelProcessManager();
        _diagnose = diagnose ?? LocalServerDiagnostics.CheckAsync; _ready = ready ?? ReadyAsync; _probe = probe ?? ProbePublicAsync;
        _inspect = inspect ?? TunnelProcessManager.InspectAsync;
    }

    [GeneratedRegex(@"https://[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.trycloudflare\.com(?![a-z0-9.\-])", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
    public static string? ExtractUrl(string line) => UrlPattern().Match(line) is { Success: true } match ? match.Value : null;

    public bool Start(AppSettings settings)
    {
        settings.Validate();
        lock (_gate)
        {
            if (!_run.IsCompleted) return false;
            _session?.Dispose();
            _session = new();
            var token = _session.Token;
            Set(new(TunnelState.Validating, "로컬 서버에 연결할 수 있는지 확인합니다."));
            _run = Task.Run(() => RunAsync(settings, token));
            return true;
        }
    }

    public async Task StopAsync()
    {
        Task run;
        lock (_gate)
        {
            run = _run;
            if (run.IsCompleted) { Set(new(TunnelState.Idle, "터널이 중지되었습니다.")); return; }
            Set(new(TunnelState.Stopping, "실행 중인 작업과 터널을 안전하게 종료합니다."));
            _session?.Cancel();
        }
        await run.ConfigureAwait(false);
    }

    private void Set(TunnelSnapshot snapshot)
    {
        lock (_gate)
        {
            if (!TunnelStateMachine.CanTransition(_snapshot.State, snapshot.State))
                throw new InvalidOperationException($"Invalid tunnel transition: {_snapshot.State} -> {snapshot.State}");
            _snapshot = snapshot; Changed?.Invoke(snapshot);
        }
    }
    private void Update(TunnelSnapshot snapshot, CancellationToken token)
    {
        lock (_gate) { if (!token.IsCancellationRequested) Set(snapshot); }
    }

    private async Task RunAsync(AppSettings settings, CancellationToken token)
    {
        try
        {
            var diagnostic = await _diagnose(settings, token).ConfigureAwait(false);
            _logs.Write(diagnostic.Success ? diagnostic.Message : diagnostic.Message + " 터널 주소는 먼저 발급하고 로컬 서버를 기다립니다.", diagnostic.Success ? "INFO" : "WARN");
            if (diagnostic.Details is not null && settings.LogLevel == "debug") _logs.Write(diagnostic.Details, "DEBUG");
            if (!File.Exists(_paths.Executable)) throw new FileNotFoundException("cloudflared가 없습니다. 고급 설정에서 설치하세요.");
            var help = await _inspect(_paths.Executable, ["tunnel", "--help"], token).ConfigureAwait(false);
            if (settings.EmailList.Length > 0 && !help.Contains("--allowed-mail", StringComparison.Ordinal))
                throw new ArgumentException("설치된 cloudflared는 이메일 접근 제한을 지원하지 않습니다. 최신 버전으로 업데이트하세요. 공개 터널로 대신 실행하지 않습니다.");
            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try { await RunAttemptAsync(settings, attempt, token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logs.Write(ex.ToString(), "ERROR");
                    if (!settings.AutoReconnect || attempt >= settings.ReconnectLimit) throw;
                    Update(new(TunnelState.Reconnecting, $"연결을 복구합니다. 재시도 {attempt + 1}/{settings.ReconnectLimit}", Retry: attempt + 1), token);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(15, (attempt + 1) * 2)), token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logs.Write(ex.ToString(), "ERROR");
            var message = ex switch
            {
                ArgumentException or FileNotFoundException => ex.Message,
                System.ComponentModel.Win32Exception => "cloudflared를 실행할 수 없습니다. 파일 손상 또는 보안 프로그램 차단 여부를 확인하고 다시 설치하세요.",
                IOException => ex.Message + " 자세한 내용은 로그를 확인하세요.",
                _ => "Cloudflare 연결에 실패했습니다. 인터넷과 방화벽의 7844 포트를 확인하세요. 자세한 내용은 로그에서 확인할 수 있습니다."
            };
            Update(new(TunnelState.Error, message), token);
        }
        finally { if (token.IsCancellationRequested) Set(new(TunnelState.Idle, "터널이 중지되었습니다. 이전 공개 주소는 사용할 수 없습니다.")); }
    }

    public static IReadOnlyList<string> BuildArguments(AppSettings settings, int metricsPort, string configPath)
    {
        settings.Validate();
        var args = new List<string> { "tunnel", "--config", configPath, "--no-autoupdate", "--url", settings.LocalUrl,
            "--metrics", $"127.0.0.1:{metricsPort}", "--loglevel", settings.LogLevel, "--protocol", settings.Protocol };
        if (settings.ServiceType == "HTTPS" && settings.NoTlsVerify) args.Add("--no-tls-verify");
        foreach (var email in settings.EmailList) { args.Add("--allowed-mail"); args.Add(email); }
        return args;
    }

    private async Task RunAttemptAsync(AppSettings settings, int attempt, CancellationToken token)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Directory.CreateDirectory(_paths.Root);
        var config = Path.Combine(_paths.Root, "quick-tunnel.yml");
        await File.WriteAllTextAsync(config, "# Managed Quick Tunnel configuration; ignores global named-tunnel settings.\n", token).ConfigureAwait(false);
        Update(new(TunnelState.Starting, "cloudflared를 실행합니다.", Retry: attempt), token);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        string? url = null;
        var task = _process.RunAsync(_paths.Executable, BuildArguments(settings, port, config), line =>
        {
            var extracted = ExtractUrl(line);
            if (extracted is not null) Interlocked.CompareExchange(ref url, extracted, null);
            var level = line.Contains(" ERR ", StringComparison.Ordinal) ? "ERROR" : line.Contains(" WRN ", StringComparison.Ordinal) ? "WARN" : line.Contains(" DBG ", StringComparison.Ordinal) ? "DEBUG" : "INFO";
            if (LevelRank(level) >= LevelRank(settings.LogLevel)) _logs.Write(line, level);
        }, lifetime.Token);
        var since = DateTimeOffset.UtcNow;
        Update(new(TunnelState.Connecting, "공개 주소와 Cloudflare 연결을 기다립니다.", Retry: attempt), token);
        DateTimeOffset? connectedAt = null;
        int failures = 0;
        try
        {
            while (!task.IsCompleted)
            {
                token.ThrowIfCancellationRequested();
                var currentUrl = Volatile.Read(ref url);
                // /quicktunnel works even when WARN/ERROR logging suppresses the startup URL.
                if (currentUrl is null) currentUrl = await ReadQuickUrlAsync(port, token).ConfigureAwait(false);
                if (currentUrl is not null) Interlocked.CompareExchange(ref url, currentUrl, null);
                var ready = currentUrl is not null && await _ready(port, token).ConfigureAwait(false);
                DiagnosticResult local = new(false, "로컬 서버 확인 대기");
                DiagnosticResult result = new(false, currentUrl is null ? "공개 주소 발급을 기다립니다." : "주소가 발급되었습니다. 실제 연결을 확인합니다.");
                if (ready)
                {
                    failures = 0;
                    local = await _diagnose(settings, token).ConfigureAwait(false);
                    result = local.Success ? await _probe(currentUrl!, token).ConfigureAwait(false) : local;
                }
                if (result.Details is not null && settings.LogLevel == "debug") _logs.Write(result.Details, "DEBUG");
                if (ready && local.Success && result.Success)
                {
                    failures = 0;
                    connectedAt ??= DateTimeOffset.UtcNow;
                    Update(new(TunnelState.Connected, "터널과 로컬 서버가 연결되었습니다.", currentUrl, connectedAt, attempt, result.Message, EdgeReady: true, LocalReady: true), token);
                }
                else
                {
                    connectedAt = null;
                    var message = ready && !local.Success
                        ? $"{settings.LocalUrl}에서 서버를 기다립니다. 웹 서버를 실행하면 같은 터널 주소로 연결됩니다."
                        : result.Message;
                    Update(new(Snapshot.State == TunnelState.Connected || Snapshot.State == TunnelState.Reconnecting || failures > 0 ? TunnelState.Reconnecting : TunnelState.Connecting,
                        message, currentUrl, Retry: attempt, Reachability: result.Message, EdgeReady: ready, LocalReady: local.Success), token);
                    // An idle origin or client-side DNS failure must not destroy an already established tunnel URL.
                    if (!ready && DateTimeOffset.UtcNow - since > TimeSpan.FromSeconds(60) && ++failures >= 3)
                        throw new IOException("Cloudflare 연결을 확인할 수 없습니다. 인터넷과 방화벽 설정을 확인하세요.");
                }
                await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            }
            var code = await task.ConfigureAwait(false);
            throw new IOException($"cloudflared가 예기치 않게 종료되었습니다. 종료 코드: {code}");
        }
        finally
        {
            lifetime.Cancel();
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
    }

    private static int LevelRank(string level) => level.ToLowerInvariant() switch { "debug" => 0, "info" => 1, "warn" => 2, "error" => 3, "fatal" => 4, _ => 1 };
    private static readonly HttpClient LoopbackClient = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
    private static readonly HttpClient PublicClient = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
    private static async Task<string?> ReadQuickUrlAsync(int port, CancellationToken token)
    {
        try
        {
            using var response = await LoopbackClient.GetAsync($"http://127.0.0.1:{port}/quicktunnel", token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            return json.RootElement.TryGetProperty("hostname", out var host) ? ExtractUrl("https://" + host.GetString()) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException) { return null; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
    }
    private static async Task<bool> ReadyAsync(int port, CancellationToken token)
    {
        try { using var response = await LoopbackClient.GetAsync($"http://127.0.0.1:{port}/ready", token).ConfigureAwait(false); return response.IsSuccessStatusCode; }
        catch (HttpRequestException) { return false; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
    }
    public static async Task<DiagnosticResult> ProbePublicAsync(string url, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await PublicClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            var code = (int)response.StatusCode;
            return new(code is not (502 or 503 or 530) && code < 520, code is >= 300 and < 400
                ? $"공개 주소 HTTP {code} 응답 확인. 리디렉션·이메일 인증 이후의 페이지는 별도 확인이 필요합니다."
                : $"공개 주소 HTTP {code} 응답 확인. 앱의 모든 기능이 정상임을 보장하지는 않습니다.");
        }
        catch (HttpRequestException ex) { return new(false, ex.HttpRequestError == HttpRequestError.NameResolutionError
            ? "공개 주소의 DNS를 아직 확인할 수 없습니다. DNS 전파 및 DNS 서버 설정을 확인하세요."
            : "공개 주소에 연결할 수 없습니다. 인터넷·프록시·TLS 연결 상태를 확인하세요.", ex.ToString()); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(false, "공개 주소 응답을 기다리는 중입니다."); }
    }

    public async ValueTask DisposeAsync() { await StopAsync().ConfigureAwait(false); _session?.Dispose(); }
}
