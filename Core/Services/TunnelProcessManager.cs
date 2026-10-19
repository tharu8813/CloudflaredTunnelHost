using System.Diagnostics;
using System.Text;

namespace CloudflaredTunnelHost.Core.Services;

public interface ITunnelProcessManager
{
    Task<int> RunAsync(string executable, IReadOnlyList<string> arguments, Action<string> output, CancellationToken token);
}

public sealed class TunnelProcessManager : ITunnelProcessManager
{
    public async Task<int> RunAsync(string executable, IReadOnlyList<string> arguments, Action<string> output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        // Do not let ambient TUNNEL_* environment variables change the explicit GUI settings.
        foreach (var key in info.Environment.Keys.Where(x => x.StartsWith("TUNNEL_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        using var process = new Process { StartInfo = info };
        if (!process.Start()) throw new IOException("cloudflared 프로세스를 실행할 수 없습니다.");
        async Task PumpAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line) output(line);
        }
        var stdout = PumpAsync(process.StandardOutput);
        var stderr = PumpAsync(process.StandardError);
        try
        {
            using var job = ProcessJob.Attach(process);
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        }
        return process.ExitCode;
    }

    public static async Task<string> InspectAsync(string executable, string[] arguments, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var text = new StringBuilder();
        var gate = new object();
        var code = await new TunnelProcessManager().RunAsync(executable, arguments, line =>
        {
            lock (gate) { if (text.Length < 128 * 1024) text.AppendLine(line); }
        }, timeout.Token).ConfigureAwait(false);
        if (code != 0) throw new IOException("cloudflared 실행 파일 검증에 실패했습니다.");
        return text.ToString().Trim();
    }
}
