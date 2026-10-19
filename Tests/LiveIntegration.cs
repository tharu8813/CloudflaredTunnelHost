using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.Core.Services;

internal static class LiveIntegration
{
    public static async Task RunAsync(bool requirePublicResponse = true)
    {
        var paths = new AppPaths(Path.GetFullPath(".test-data/live"));
        var logs = new LoggingService(paths) { SaveToFile = true };
        var download = new DownloadService(paths);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var release = await download.CheckUpdateAsync(timeout.Token);
        Console.WriteLine("Official latest: " + release.Version);
        try { await DownloadService.VerifyAsync(paths.Executable, release.Sha256, timeout.Token); }
        catch (IOException) { await download.InstallAsync(release, null, timeout.Token); }
        Console.WriteLine(await TunnelProcessManager.InspectAsync(paths.Executable, ["--version"], timeout.Token));
        var help = await TunnelProcessManager.InspectAsync(paths.Executable, ["tunnel", "--help"], timeout.Token);
        Console.WriteLine("allowed-mail supported: " + help.Contains("--allowed-mail"));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        listener = new TcpListener(IPAddress.Loopback, port);
        using var serverLifetime = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        Task server = Task.CompletedTask;
        await using var service = new CloudflaredService(paths, logs);
        try
        {
            service.Changed += snapshot => Console.WriteLine($"State={snapshot.State} URLAllocated={snapshot.PublicUrl is not null} {snapshot.Message}");
            service.Start(new() { Port = port, AutoReconnect = false });
            var watch = Stopwatch.StartNew();
            while (!service.Snapshot.EdgeReady)
            {
                if (service.Snapshot.State == TunnelState.Error) throw new Exception(service.Snapshot.Message);
                if (watch.Elapsed > TimeSpan.FromSeconds(100)) throw new TimeoutException("Quick Tunnel allocation timed out.");
                await Task.Delay(300, timeout.Token);
            }
            var allocatedUrl = service.Snapshot.PublicUrl;
            if (allocatedUrl is null || service.Snapshot.LocalReady || service.Snapshot.State == TunnelState.Connected) throw new Exception("Offline origin was reported incorrectly.");
            Console.WriteLine("PASS tunnel URL allocated while no local server is running");
            listener.Start(); server = ServeAsync(listener, serverLifetime.Token);
            while (!service.Snapshot.LocalReady)
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(120)) throw new TimeoutException("Late local origin detection timed out.");
                await Task.Delay(300, timeout.Token);
            }
            if (service.Snapshot.PublicUrl != allocatedUrl) throw new Exception("Late origin startup changed the public URL.");
            Console.WriteLine("PASS late local server startup is detected without replacing the tunnel URL");
            if (!requirePublicResponse)
            {
                Console.WriteLine("SKIP external public response verification (origin-late test mode)");
                await service.StopAsync();
                if (service.Snapshot.IsActive || service.Snapshot.PublicUrl is not null) throw new Exception("Stop did not invalidate URL.");
                Console.WriteLine("PASS real cloudflared stop and URL invalidation");
                return;
            }
            while (service.Snapshot.State is not (TunnelState.Connected or TunnelState.Error))
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (watch.Elapsed > TimeSpan.FromSeconds(100)) throw new TimeoutException("Public tunnel readiness timed out.");
                await Task.Delay(300, timeout.Token);
            }
            if (service.Snapshot.State != TunnelState.Connected) throw new Exception(service.Snapshot.Message);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var body = await http.GetStringAsync(service.Snapshot.PublicUrl, timeout.Token);
            if (!body.Contains("CloudflaredTunnelHost integration fixture")) throw new Exception("The public response did not match the test-only local server.");
            Console.WriteLine("PASS public URL returns the test-only local page");
            await service.StopAsync();
            if (service.Snapshot.State != TunnelState.Idle || service.Snapshot.PublicUrl is not null) throw new Exception("Stop did not invalidate URL.");
            Console.WriteLine("PASS real cloudflared start, public readiness, URL routing, stop and URL invalidation");
        }
        finally
        {
            await service.StopAsync(); serverLifetime.Cancel(); listener.Stop();
            try { await server; } catch (OperationCanceledException) { }
        }
    }
    private static async Task ServeAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync(token);
            await using var stream = client.GetStream();
            var buffer = new byte[8192];
            var count = await stream.ReadAsync(buffer, token);
            var head = Encoding.ASCII.GetString(buffer, 0, count).StartsWith("HEAD ", StringComparison.Ordinal);
            const string body = "CloudflaredTunnelHost integration fixture";
            var header = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header + (head ? "" : body)), token);
        }
    }
}
