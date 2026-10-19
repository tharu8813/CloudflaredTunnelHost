using System.Net.Http;
using System.Security.Authentication;
using System.Net.Sockets;
using CloudflaredTunnelHost.Core.Models;

namespace CloudflaredTunnelHost.Core.Services;

public static class LocalServerDiagnostics
{
    public static async Task<DiagnosticResult> CheckAsync(AppSettings settings, CancellationToken token)
    {
        settings.Validate();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        if (settings.ServiceType == "HTTPS" && settings.NoTlsVerify)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, settings.LocalUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            return new(true, $"로컬 서버가 HTTP {(int)response.StatusCode} 응답을 반환했습니다. 인증 오류나 404 응답도 서버 연결 확인에는 유효합니다.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return new(false, "로컬 서버 응답 시간이 초과됐습니다. 서버가 실행 중인지, 선택한 HTTP/HTTPS와 포트가 맞는지 확인하세요."); }
        catch (HttpRequestException ex)
        {
            var tls = ex.InnerException is AuthenticationException || ex.HttpRequestError == HttpRequestError.SecureConnectionError;
            var refused = ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused };
            return new(false, tls
                ? "HTTPS 인증서를 확인할 수 없습니다. 신뢰할 수 있는 개발 인증서를 설치하세요. 자체 서명 인증서인 경우에만 고급 설정의 인증서 검증 옵션을 검토하세요."
                : refused ? $"{settings.LocalUrl}에서 연결을 거부했습니다. 개발 서버를 실행하고 서버에 표시된 포트를 입력하세요. 이 앱은 웹 서버를 직접 실행하지 않습니다."
                : $"{settings.LocalUrl}에 연결할 수 없습니다. 개발 서버의 실행 상태와 HTTP/HTTPS 및 포트를 확인하세요.", ex.ToString());
        }
    }
}
