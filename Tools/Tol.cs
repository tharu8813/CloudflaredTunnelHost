using System.Net;

namespace CloudflaredTunnelHost.Tools
{
    internal class Tol
    {

        public static event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;
        public static string path = Application.StartupPath;

        public static void ShowError(string text)
        {
            MessageBox.Show(text, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowInfo(string text)
        {
            MessageBox.Show(text, "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowWarning(string text)
        {
            MessageBox.Show(text, "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static DialogResult ShowQuestion(string text)
        {
            return MessageBox.Show(text, "질문", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }


        // 다운로드 완료 이벤트

        /// <summary>
        /// 파일을 다운로드하고 진행 상황을 UI에 표시합니다.
        /// </summary>
        /// <param name="downloadPath">저장할 파일 경로 (파일명 포함)</param>
        /// <param name="downloadUrl">다운로드할 파일의 URL</param>
        /// <param name="progressBar">진행률을 표시할 ProgressBar (선택)</param>
        /// <param name="label">상태를 표시할 Label (선택)</param>
        /// <returns>다운로드 성공 여부</returns>
        public static async Task<bool> DownloadFileAsync(string downloadPath, string downloadUrl,
                                                  ProgressBar progressBar = null, Label label = null)
        {
            bool success = false;
            string errorMessage = null;

            try
            {
                // 디렉토리가 없으면 생성
                string directory = Path.GetDirectoryName(downloadPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (WebClient client = new WebClient())
                {
                    // 다운로드 진행률 변경 이벤트
                    client.DownloadProgressChanged += (sender, e) =>
                    {
                        if (progressBar != null)
                        {
                            progressBar.Value = e.ProgressPercentage;
                        }

                        if (label != null)
                        {
                            label.Text = $"다운로드 중... {e.ProgressPercentage}% " +
                                       $"({FormatBytes(e.BytesReceived)} / {FormatBytes(e.TotalBytesToReceive)})";
                        }
                    };

                    // 초기 상태 설정
                    if (progressBar != null)
                    {
                        progressBar.Value = 0;
                    }
                    if (label != null)
                    {
                        label.Text = "다운로드 시작...";
                    }

                    // 비동기 다운로드 시작
                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), downloadPath);

                    // 다운로드 성공
                    success = true;

                    if (progressBar != null)
                    {
                        progressBar.Value = 100;
                    }
                    if (label != null)
                    {
                        label.Text = "다운로드 완료!";
                    }
                    MessageBox.Show("파일 다운로드가 완료되었습니다.",
                                  "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;

                if (label != null)
                {
                    label.Text = $"오류: {ex.Message}";
                }
                MessageBox.Show($"다운로드 중 오류가 발생했습니다: {ex.Message}",
                              "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 다운로드 완료 이벤트 발생
                OnDownloadCompleted(new DownloadCompletedEventArgs
                {
                    Success = success,
                    FilePath = downloadPath,
                    DownloadUrl = downloadUrl,
                    ErrorMessage = errorMessage
                });
            }

            return success;
        }

        /// <summary>
        /// 바이트를 읽기 쉬운 형식으로 변환합니다.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 다운로드 완료 이벤트를 발생시킵니다.
        /// </summary>
        protected static virtual void OnDownloadCompleted(DownloadCompletedEventArgs e)
        {
            DownloadCompleted?.Invoke(this, e);
        }
    }
}

/// <summary>
/// 다운로드 완료 이벤트 인자
/// </summary>
public class DownloadCompletedEventArgs : EventArgs
{
    public bool Success { get; set; }
    public string FilePath { get; set; }
    public string DownloadUrl { get; set; }
    public string ErrorMessage { get; set; }
}