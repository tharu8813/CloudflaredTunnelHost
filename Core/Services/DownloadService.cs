using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CloudflaredTunnelHost.Core.Services;

public sealed record CloudflaredRelease(string Version, Uri DownloadUrl, string Sha256, long Size);

public sealed class DownloadService(AppPaths paths, HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? CreateClient();
    private readonly SemaphoreSlim _gate = new(1, 1);
    public static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CloudflaredTunnelHost/2.0");
        return http;
    }

    public async Task<CloudflaredRelease> CheckUpdateAsync(CancellationToken token)
    {
        using var response = await _client.GetAsync("https://api.github.com/repos/cloudflare/cloudflared/releases/latest", token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
        var root = json.RootElement;
        const string filename = "cloudflared-windows-amd64.exe";
        var asset = root.GetProperty("assets").EnumerateArray().FirstOrDefault(x => x.GetProperty("name").GetString() == filename);
        if (asset.ValueKind == JsonValueKind.Undefined) throw new IOException("공식 Windows 실행 파일을 찾을 수 없습니다.");
        var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        if (url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith("/cloudflare/cloudflared/releases/download/", StringComparison.Ordinal))
            throw new IOException("공식 배포 경로가 아닙니다.");
        var hash = asset.TryGetProperty("digest", out var digest) ? digest.GetString()?.Replace("sha256:", "", StringComparison.Ordinal) : null;
        if (string.IsNullOrEmpty(hash))
        {
            var match = Regex.Match(root.GetProperty("body").GetString() ?? "", Regex.Escape(filename) + @":\s*([a-fA-F0-9]{64})\b");
            hash = match.Success ? match.Groups[1].Value : null;
        }
        if (hash is null || !Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$"))
            throw new IOException("공식 SHA256 검증 정보가 없어 다운로드를 중단했습니다. 기존 버전은 보존됩니다.");
        var size = asset.GetProperty("size").GetInt64();
        if (size is <= 0 or > 150_000_000) throw new IOException("실행 파일 크기가 유효하지 않습니다.");
        return new(root.GetProperty("tag_name").GetString()!, url, hash, size);
    }

    public async Task InstallAsync(CloudflaredRelease release, IProgress<double>? progress, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        var temp = paths.Executable + "." + Guid.NewGuid().ToString("N") + ".download.exe";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.Executable)!);
            using var response = await _client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > release.Size) throw new IOException("다운로드 파일의 크기가 공식 정보와 다릅니다.");
                    await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    progress?.Report(total * 100.0 / release.Size);
                }
                if (total != release.Size) throw new IOException("다운로드가 완전하지 않습니다. 다시 시도하세요.");
                await destination.FlushAsync(token).ConfigureAwait(false);
            }
            await VerifyAsync(temp, release.Sha256, token).ConfigureAwait(false);
            var version = await TunnelProcessManager.InspectAsync(temp, ["--version"], token).ConfigureAwait(false);
            if (!version.StartsWith("cloudflared version ", StringComparison.OrdinalIgnoreCase) || !version.Contains(release.Version.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                throw new IOException("다운로드된 실행 파일의 버전이 공식 정보와 다릅니다.");
            token.ThrowIfCancellationRequested();
            if (File.Exists(paths.Executable)) File.Replace(temp, paths.Executable, paths.Executable + ".bak");
            else File.Move(temp, paths.Executable);
            progress?.Report(100);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            finally { _gate.Release(); }
        }
    }

    public static async Task VerifyAsync(string path, string expectedHash, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false));
        if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("SHA256이 일치하지 않습니다. 파일을 설치하지 않았습니다.");
        file.Position = 0;
        var header = new byte[64];
        if (await file.ReadAsync(header, token).ConfigureAwait(false) != header.Length || header[0] != 'M' || header[1] != 'Z')
            throw new IOException("Windows 실행 파일이 아닙니다.");
        var offset = BitConverter.ToInt32(header, 60);
        if (offset < 64 || offset > file.Length - 6) throw new IOException("실행 파일 헤더가 손상됐습니다.");
        file.Position = offset;
        var pe = new byte[6];
        await file.ReadExactlyAsync(pe, token).ConfigureAwait(false);
        if (pe[0] != 'P' || pe[1] != 'E' || pe[2] != 0 || pe[3] != 0 || BitConverter.ToUInt16(pe, 4) != 0x8664)
            throw new IOException("지원하는 Windows x64 실행 파일이 아닙니다.");
    }
}
