using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.Core.Services;

if (args.Contains("--live")) { await LiveIntegration.RunAsync(); return; }
if (args.Contains("--live-origin-late")) { await LiveIntegration.RunAsync(requirePublicResponse: false); return; }

var root = Path.GetFullPath(Path.Combine(".test-data", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
int passed = 0;
var failures = new List<string>();
await Check("settings validation covers ports, enums, retries and recipients", () =>
{
    foreach (var invalid in new[] { new AppSettings { Port = 0 }, new AppSettings { Port = 65536 }, new AppSettings { ServiceType = "tcp" }, new AppSettings { Protocol = "bad" }, new AppSettings { ReconnectLimit = 11 }, new AppSettings { AllowedEmails = "bad" }, new AppSettings { AllowedEmails = "Name <a@example.com>" }, new AppSettings { SchemaVersion = 99 } })
        Throws<ArgumentException>(invalid.Validate);
    new AppSettings { Port = 65535, AllowedEmails = "one@example.com, two@example.com" }.Validate();
    return Task.CompletedTask;
});
await Check("legacy settings migrate without deleting the source", () =>
{
    var paths = NewPaths();
    File.WriteAllText(paths.LegacySettings, "Port=3000\nServiceType=HTTPS\nLogLevel=debug\nProtocol=http2\nHttpsNoVerify=True\nLogFile=True\n");
    var storage = new SettingsService(paths); var settings = storage.Load();
    Assert(settings.Port == 3000 && settings.ServiceType == "HTTPS" && settings.NoTlsVerify && settings.SaveLogFile && settings.Protocol == "http2");
    Assert(File.Exists(paths.Settings) && File.Exists(paths.LegacySettings));
    storage.Save(settings with { Port = 8080 }); Assert(storage.Load().Port == 8080 && File.Exists(paths.Settings + ".bak"));
    return Task.CompletedTask;
});
await Check("invalid legacy values fall back safely and duplicates are tolerated", () =>
{
    var paths = NewPaths(); File.WriteAllText(paths.LegacySettings, "Port=bad\nLogLevel=Default\nPort=-1\nProtocol=DEFAULT\n");
    var settings = new SettingsService(paths).Load(); Assert(settings.Port == 8000 && settings.LogLevel == "info" && settings.Protocol == "auto");
    return Task.CompletedTask;
});
await Check("corrupt JSON is preserved and reported", () =>
{
    var paths = NewPaths(); File.WriteAllText(paths.Settings, "{bad");
    var storage = new SettingsService(paths); Assert(storage.Load().Port == 8000 && storage.Notice is not null && File.ReadAllText(paths.Settings) == "{bad");
    File.WriteAllText(paths.Settings, "{\"AllowedEmails\":null}");
    Assert(storage.Load().Port == 8000 && storage.Notice is not null);
    return Task.CompletedTask;
});
await Check("URL parser accepts Quick Tunnel URLs and rejects lookalike hosts", () =>
{
    Assert(CloudflaredService.ExtractUrl("INF | https://small-test.trycloudflare.com |") == "https://small-test.trycloudflare.com");
    foreach (var text in new[] { "https://x.trycloudflare.com.evil.org", "http://x.trycloudflare.com", "https://trycloudflare.com", "https://x.trycloudflare.com-other" }) Assert(CloudflaredService.ExtractUrl(text) is null);
    return Task.CompletedTask;
});
await Check("CLI arguments preserve secure defaults and explicit settings", () =>
{
    var args = CloudflaredService.BuildArguments(new() { ServiceType = "HTTPS", Port = 443, LogLevel = "error", AllowedEmails = "a@example.com,b@example.com" }, 12345, "C:\\path with spaces\\quick.yml");
    Assert(args.Contains("error") && args.Contains("--no-autoupdate") && args.Contains("https://localhost:443"));
    Assert(args.Count(x => x == "--allowed-mail") == 2 && !args.Contains("--no-tls-verify"));
    Assert(CloudflaredService.BuildArguments(new() { NoTlsVerify = true }, 1234, "c").All(x => x != "--no-tls-verify"));
    return Task.CompletedTask;
});
await Check("state machine rejects a direct Idle to Connected transition", () =>
{
    Assert(!TunnelStateMachine.CanTransition(TunnelState.Idle, TunnelState.Connected));
    Assert(!TunnelStateMachine.CanTransition(TunnelState.Stopping, TunnelState.Connected));
    Assert(TunnelStateMachine.CanTransition(TunnelState.Error, TunnelState.Validating));
    Assert(TunnelStateMachine.CanTransition(TunnelState.Connected, TunnelState.Reconnecting));
    return Task.CompletedTask;
});
await Check("log buffers are bounded and exported values are redacted", () =>
{
    var logs = new LoggingService(NewPaths());
    for (int i = 0; i < 10000; i++) logs.Write($"message {i}");
    Assert(logs.PendingCount <= 5000 && logs.Drain(50).Count == 50);
    var redacted = LoggingService.Redact("https://test.trycloudflare.com bare.trycloudflare.com a@example.com token=secret123 Authorization: Bearer BearerValue Authorization: Basic BasicValue");
    Assert(!redacted.Contains("test.trycloudflare") && !redacted.Contains("bare.trycloudflare") && !redacted.Contains("a@example.com") && !redacted.Contains("secret123") && !redacted.Contains("BearerValue") && !redacted.Contains("BasicValue"));
    logs.Clear(); Assert(logs.PendingCount == 0);
    return Task.CompletedTask;
});
await Check("rotating log file keeps the previous segment", () =>
{
    var paths = NewPaths(); Directory.CreateDirectory(paths.Logs);
    var path = Path.Combine(paths.Logs, "cloudflared.log");
    using (var file = File.Create(path)) file.SetLength(5 * 1024 * 1024 + 1);
    var logs = new LoggingService(paths) { SaveToFile = true }; logs.Write("new segment");
    Assert(File.Exists(path + ".1") && File.ReadAllText(path).Contains("new segment"));
    return Task.CompletedTask;
});
await Check("HTTP diagnosis accepts a reachable application 404", async () =>
{
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(async () =>
    {
        using var connection = await listener.AcceptTcpClientAsync();
        await using var stream = connection.GetStream();
        var request = new byte[4096]; await stream.ReadAsync(request);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
    });
    try { var result = await LocalServerDiagnostics.CheckAsync(new() { Port = port }, default); Assert(result.Success && result.Message.Contains("404")); await server; }
    finally { listener.Stop(); }
});
await Check("closed local port produces an actionable error", async () =>
{
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var result = await LocalServerDiagnostics.CheckAsync(new() { Port = port }, default); Assert(!result.Success && result.Message.Contains("개발 서버"));
});
await Check("HTTPS self-signed server requires an explicit certificate validation override", async () =>
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); request.CertificateExtensions.Add(names.Build());
    using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(async () =>
    {
        for (int i = 0; i < 2; i++)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var tls = new SslStream(client.GetStream());
            try
            {
                await tls.AuthenticateAsServerAsync(certificate, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
                var buffer = new byte[4096]; if (await tls.ReadAsync(buffer) > 0)
                    await tls.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
            }
            catch (Exception ex) when (ex is IOException or AuthenticationException) { }
        }
    });
    try
    {
        var secure = await LocalServerDiagnostics.CheckAsync(new() { Port = port, ServiceType = "HTTPS" }, default);
        Assert(!secure.Success && secure.Message.Contains("인증서"));
        var overridden = await LocalServerDiagnostics.CheckAsync(new() { Port = port, ServiceType = "HTTPS", NoTlsVerify = true }, default);
        Assert(overridden.Success); await server.WaitAsync(TimeSpan.FromSeconds(10));
    }
    finally { listener.Stop(); }
});
await Check("download release metadata verifies source and published checksum", async () =>
{
    var paths = NewPaths();
    const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"tag_name\":\"2026.1.0\",\"body\":\"cloudflared-windows-amd64.exe: " + hash + "\",\"assets\":[{\"name\":\"cloudflared-windows-amd64.exe\",\"size\":100,\"browser_download_url\":\"https://github.com/cloudflare/cloudflared/releases/download/2026.1.0/cloudflared-windows-amd64.exe\"}]}" ) }));
    var release = await new DownloadService(paths, http).CheckUpdateAsync(default); Assert(release.Sha256 == hash && release.Version == "2026.1.0");
});
await Check("download without checksum fails closed", async () =>
{
    using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"tag_name\":\"x\",\"body\":\"\",\"assets\":[{\"name\":\"cloudflared-windows-amd64.exe\",\"size\":100,\"browser_download_url\":\"https://github.com/cloudflare/cloudflared/releases/download/x/a.exe\"}]}" ) }));
    await ThrowsAsync<IOException>(() => new DownloadService(NewPaths(), http).CheckUpdateAsync(default));
});
await Check("hash mismatch preserves installed binary and cleans temporary file", async () =>
{
    var paths = NewPaths(); Directory.CreateDirectory(Path.GetDirectoryName(paths.Executable)!); File.WriteAllText(paths.Executable, "known good");
    using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[100]) }));
    await ThrowsAsync<IOException>(() => new DownloadService(paths, http).InstallAsync(Release(new string('a', 64), 100), null, default));
    Assert(File.ReadAllText(paths.Executable) == "known good" && Directory.GetFiles(Path.GetDirectoryName(paths.Executable)!, "*.download.exe").Length == 0);
});
await Check("cancel during streaming preserves installed binary and cleans temporary file", async () =>
{
    var paths = NewPaths(); Directory.CreateDirectory(Path.GetDirectoryName(paths.Executable)!); File.WriteAllText(paths.Executable, "known good");
    using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new SlowStream()) }));
    using var cancel = new CancellationTokenSource(150);
    await ThrowsAsync<OperationCanceledException>(() => new DownloadService(paths, http).InstallAsync(Release(new string('a', 64), 2048), null, cancel.Token));
    Assert(File.ReadAllText(paths.Executable) == "known good" && Directory.GetFiles(Path.GetDirectoryName(paths.Executable)!, "*.download.exe").Length == 0);
});
await Check("binary validation rejects HTML even with matching hash", async () =>
{
    var path = Path.Combine(root, "html.exe"); await File.WriteAllTextAsync(path, new string('x', 100));
    var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
    await ThrowsAsync<IOException>(() => DownloadService.VerifyAsync(path, hash, default));
});
await Check("offline local server still receives a tunnel URL and connects later without a restart", async () =>
{
    var fake = new FakeProcess(); bool localOnline = false;
    await using var service = Service(ExecutablePaths(), fake, (_, _) => Task.FromResult(new DiagnosticResult(localOnline, localOnline ? "HTTP 200" : "server offline")));
    Assert(service.Start(new())); await Until(() => service.Snapshot.EdgeReady && service.Snapshot.PublicUrl is not null);
    var url = service.Snapshot.PublicUrl;
    Assert(fake.Starts == 1 && service.Snapshot.IsActive && !service.Snapshot.LocalReady && service.Snapshot.State != TunnelState.Connected);
    localOnline = true;
    await Until(() => service.Snapshot.State == TunnelState.Connected);
    Assert(service.Snapshot.LocalReady && service.Snapshot.PublicUrl == url && fake.Starts == 1);
    localOnline = false;
    await Until(() => !service.Snapshot.LocalReady);
    Assert(service.Snapshot.IsActive && service.Snapshot.PublicUrl == url && fake.Starts == 1);
    await service.StopAsync();
});
await Check("unsupported protected access never falls back to a public tunnel", async () =>
{
    var paths = ExecutablePaths(); var fake = new FakeProcess();
    await using var service = Service(paths, fake, inspect: (_, _, _) => Task.FromResult("old help"));
    service.Start(new() { AllowedEmails = "a@example.com" }); await Until(() => service.Snapshot.State == TunnelState.Error);
    Assert(fake.Starts == 0 && service.Snapshot.Message.Contains("지원하지"));
});
await Check("URL allocation alone is not Connected; stop invalidates URL", async () =>
{
    var fake = new FakeProcess(); var states = new List<TunnelState>();
    await using var service = Service(ExecutablePaths(), fake, ready: (_, _) => Task.FromResult(false));
    service.Changed += s => { lock (states) states.Add(s.State); };
    Assert(service.Start(new()) && !service.Start(new()));
    await Until(() => service.Snapshot.PublicUrl is not null);
    Assert(service.Snapshot.State == TunnelState.Connecting && service.Snapshot.ConnectedAt is null);
    await service.StopAsync(); Assert(service.Snapshot.State == TunnelState.Idle && service.Snapshot.PublicUrl is null && fake.Active == 0);
    Assert(states.Contains(TunnelState.Validating) && states.Contains(TunnelState.Starting) && states.Contains(TunnelState.Stopping));
});
await Check("readiness plus public response produces Connected; stale output cannot change the new session", async () =>
{
    var fake = new FakeProcess();
    await using var service = Service(ExecutablePaths(), fake);
    service.Start(new()); await Until(() => service.Snapshot.State == TunnelState.Connected);
    Assert(service.Snapshot.ConnectedAt.HasValue);
    await service.StopAsync(); var oldOutput = fake.LastOutput;
    service.Start(new()); await Until(() => service.Snapshot.State == TunnelState.Connected);
    oldOutput!("https://stale.trycloudflare.com");
    Assert(service.Snapshot.PublicUrl == "https://test.trycloudflare.com" && fake.Active == 1);
    await service.StopAsync();
});
await Check("unexpected exit recovers with a strict retry limit", async () =>
{
    var fake = new FakeProcess { ExitImmediately = true };
    await using var service = Service(ExecutablePaths(), fake);
    service.Start(new() { AutoReconnect = true, ReconnectLimit = 1 });
    await Until(() => service.Snapshot.State == TunnelState.Error, 15000);
    Assert(fake.Starts == 2 && fake.Active == 0);
});
await Check("cancel during local validation cannot launch a process", async () =>
{
    var fake = new FakeProcess();
    await using var service = Service(ExecutablePaths(), fake, async (_, token) => { await Task.Delay(10000, token); return new(true, "ok"); });
    service.Start(new()); await service.StopAsync(); Assert(service.Snapshot.State == TunnelState.Idle && fake.Starts == 0);
});
if (OperatingSystem.IsWindows()) await Check("real process cancellation kills its child tree and drains output", async () =>
{
    var child = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var cancel = new CancellationTokenSource();
    var task = new TunnelProcessManager().RunAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", "$p = Start-Process powershell.exe -ArgumentList '-NoProfile -NonInteractive -Command Start-Sleep -Seconds 60' -WindowStyle Hidden -PassThru; [Console]::WriteLine($p.Id); Start-Sleep -Seconds 60"], line => { if (int.TryParse(line, out int pid)) child.TrySetResult(pid); }, cancel.Token);
    var pid = await child.Task.WaitAsync(TimeSpan.FromSeconds(15)); cancel.Cancel();
    await ThrowsAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(10)));
    try { using var process = Process.GetProcessById(pid); Assert(process.HasExited); } catch (ArgumentException) { }
});

Console.WriteLine($"\n{passed} passed, {failures.Count} failed");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

async Task Check(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures.Add(name); Console.WriteLine("FAIL " + name + "\n" + ex); }
}
AppPaths NewPaths() { var paths = new AppPaths(Path.Combine(root, Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(paths.Root); return paths; }
AppPaths ExecutablePaths() { var paths = NewPaths(); Directory.CreateDirectory(Path.GetDirectoryName(paths.Executable)!); File.WriteAllText(paths.Executable, "fixture"); return paths; }
CloudflaredService Service(AppPaths paths, FakeProcess process, Func<AppSettings, CancellationToken, Task<DiagnosticResult>>? diagnose = null, Func<int, CancellationToken, Task<bool>>? ready = null, Func<string, string[], CancellationToken, Task<string>>? inspect = null) =>
    new(paths, new(paths), process, diagnose ?? ((_, _) => Task.FromResult(new DiagnosticResult(true, "ok"))), ready ?? ((_, _) => Task.FromResult(true)), (_, _) => Task.FromResult(new DiagnosticResult(true, "HTTP 200")), inspect ?? ((_, _, _) => Task.FromResult("--allowed-mail")));
CloudflaredRelease Release(string hash, long size) => new("test", new("https://github.com/cloudflare/cloudflared/releases/download/test/cloudflared-windows-amd64.exe"), hash, size);
static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
static async Task Until(Func<bool> predicate, int timeout = 10000) { var watch = Stopwatch.StartNew(); while (!predicate()) { if (watch.ElapsedMilliseconds > timeout) throw new TimeoutException(); await Task.Delay(25); } }

sealed class FakeProcess : ITunnelProcessManager
{
    public int Starts; public int Active; public bool ExitImmediately; public Action<string>? LastOutput;
    public async Task<int> RunAsync(string path, IReadOnlyList<string> args, Action<string> output, CancellationToken token)
    {
        Interlocked.Increment(ref Starts); Interlocked.Increment(ref Active); LastOutput = output;
        try { output("INF https://test.trycloudflare.com"); if (ExitImmediately) return 42; await Task.Delay(Timeout.Infinite, token); return 0; }
        finally { Interlocked.Decrement(ref Active); output("process exited"); }
    }
}
sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
}
sealed class SlowStream : Stream
{
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => 2048; public override long Position { get; set; }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(80, cancellationToken); buffer.Span[..128].Clear(); Position += 128; return 128; }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
