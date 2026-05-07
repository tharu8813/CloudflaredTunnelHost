// Tools/Tol.cs - 개선된 버전
using System.Net.Http;

namespace CloudflaredTunnelHost.Tools {
    internal class Tol {
        public static event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;
        public static string path = Application.StartupPath;
        public static string dataPath = Application.LocalUserAppDataPath;
        public static string cloudflaredPath = Path.Combine(dataPath, "tools", "cloudflared.exe");

        // [개선] HttpClient는 재사용을 위해 static으로 선언 (WebClient는 deprecated)
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        }) {
            Timeout = TimeSpan.FromMinutes(5) // [개선] 다운로드 타임아웃 설정
        };

        public static void ShowError(string text) {
            MessageBox.Show(text, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowInfo(string text) {
            MessageBox.Show(text, "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowWarning(string text) {
            MessageBox.Show(text, "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static DialogResult ShowQuestion(string text) {
            return MessageBox.Show(text, "질문", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        /// <summary>
        /// 파일을 다운로드하고 진행 상황을 UI에 표시합니다.
        /// </summary>
        /// <param name="downloadPath">저장할 파일 경로 (파일명 포함)</param>
        /// <param name="downloadUrl">다운로드할 파일의 URL</param>
        /// <param name="progressBar">진행률을 표시할 ProgressBar (선택)</param>
        /// <param name="label">상태를 표시할 Label (선택)</param>
        /// <param name="cancellationToken">취소 토큰 (선택)</param>
        /// <returns>다운로드 성공 여부</returns>
        public static async Task<bool> DownloadFileAsync(
            string downloadPath,
            string downloadUrl,
            ProgressBar? progressBar = null,
            Label? label = null,
            CancellationToken cancellationToken = default) {

            bool success = false;
            string? errorMessage = null;
            string? tempPath = null;

            try {
                // [버그수정] URL 유효성 검사
                if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
                    throw new ArgumentException($"유효하지 않은 URL입니다: {downloadUrl}");
                }

                // 디렉토리 생성
                string? directory = Path.GetDirectoryName(downloadPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                // [개선] 임시 파일에 먼저 다운로드 후 이동 (불완전한 파일 방지)
                tempPath = downloadPath + ".tmp";

                UpdateLabel(label, "연결 중...");
                UpdateProgress(progressBar, 0);

                using var response = await _httpClient.GetAsync(
                    uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                // [버그수정] HTTP 상태 코드 확인
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long downloadedBytes = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes.HasValue && totalBytes > 0) {
                        int percentage = (int)((double)downloadedBytes / totalBytes.Value * 100);
                        UpdateProgress(progressBar, percentage);
                        UpdateLabel(label,
                            $"다운로드 중... {percentage}% ({FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes.Value)})");
                    } else {
                        // [개선] 파일 크기를 모를 때도 진행 표시
                        UpdateLabel(label, $"다운로드 중... {FormatBytes(downloadedBytes)}");
                    }
                }

                await fileStream.FlushAsync(cancellationToken);
                fileStream.Close();

                // [개선] 임시 파일을 최종 경로로 이동 (기존 파일 덮어쓰기)
                if (File.Exists(downloadPath))
                    File.Delete(downloadPath);
                File.Move(tempPath, downloadPath);
                tempPath = null; // 이동 완료, finally에서 삭제 불필요

                success = true;
                UpdateProgress(progressBar, 100);
                UpdateLabel(label, "다운로드 완료!");

                MessageBox.Show("파일 다운로드가 완료되었습니다.",
                    "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);

            } catch (OperationCanceledException) {
                // [개선] 취소 처리
                errorMessage = "다운로드가 취소되었습니다.";
                UpdateLabel(label, errorMessage);
                // 취소는 오류 메시지 팝업 없이 처리
            } catch (HttpRequestException ex) {
                success = false;
                errorMessage = $"네트워크 오류: {ex.Message}";
                UpdateLabel(label, $"오류: {ex.Message}");
                MessageBox.Show($"다운로드 중 네트워크 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } catch (Exception ex) {
                success = false;
                errorMessage = ex.Message;
                UpdateLabel(label, $"오류: {ex.Message}");
                MessageBox.Show($"다운로드 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally {
                // [버그수정] 실패 시 임시 파일 정리
                if (tempPath != null && File.Exists(tempPath)) {
                    try { File.Delete(tempPath); } catch { }
                }

                OnDownloadCompleted(new DownloadCompletedEventArgs {
                    Success = success,
                    FilePath = downloadPath,
                    DownloadUrl = downloadUrl,
                    ErrorMessage = errorMessage
                });
            }

            return success;
        }

        // [개선] UI 업데이트 헬퍼 - InvokeRequired 처리 포함
        private static void UpdateProgress(ProgressBar? progressBar, int value) {
            if (progressBar == null) return;
            if (progressBar.InvokeRequired)
                progressBar.Invoke(new Action(() => progressBar.Value = Math.Clamp(value, 0, 100)));
            else
                progressBar.Value = Math.Clamp(value, 0, 100);
        }

        private static void UpdateLabel(Label? label, string text) {
            if (label == null) return;
            if (label.InvokeRequired)
                label.Invoke(new Action(() => label.Text = text));
            else
                label.Text = text;
        }

        /// <summary>
        /// 바이트를 읽기 쉬운 형식으로 변환합니다.
        /// </summary>
        public static string FormatBytes(long bytes) {
            // [버그수정] 음수 바이트 방어
            if (bytes < 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1) {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private static void OnDownloadCompleted(DownloadCompletedEventArgs e) {
            DownloadCompleted?.Invoke(null, e);
        }
    }

    /// <summary>
    /// 다운로드 완료 이벤트 인자
    /// </summary>
    public class DownloadCompletedEventArgs : EventArgs {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}