// MainForm.cs - 로직 코드
using CloudflaredTunnelHost.Tools;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CloudflaredTunnelHost.Start {
    public partial class MainForm : DevForm {
        private Process cloudflaredProcess;
        private bool isRunning = false;
        private string currentTunnelUrl = string.Empty;
        private string workingDirectory = string.Empty;

        public MainForm() {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e) {
            this.Text = "Cloudflared Tunnel Host";

            cmbServiceType.SelectedIndex = 0; // HTTP
            cmbLogLevel.SelectedIndex = 0; // 없음
            cmbProtocol.SelectedIndex = 0; // 없음

            workingDirectory = Application.StartupPath;
            txtWorkingDirectory.Text = workingDirectory;

            AppendLog("프로그램이 시작되었습니다.");
            AppendLog($"기본 작업 디렉토리: {workingDirectory}");
        }

        private void MainForm_Shown(object sender, EventArgs e) {
            if (!File.Exists(Tol.cloudflaredPath)) {
                if (Tol.ShowQuestion("cloudflared.exe가 존재하지 않습니다. 다운받으시겠습니까?") == DialogResult.Yes) {
                    if (new Download().ShowDialog() != DialogResult.OK) {
                        Application.Exit();
                    }
                } else {
                    Application.Exit();
                }
            } else {
                AppendLog($"cloudflared.exe 경로: {Tol.cloudflaredPath}");
            }
        }

        private void btnStart_Click(object sender, EventArgs e) {
            if (isRunning) return;

            // 포트 유효성 검사
            if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535) {
                Tol.ShowError("올바른 포트 번호를 입력해주세요 (1-65535)");
                return;
            }

            StartTunnel();
        }

        private void btnStop_Click(object sender, EventArgs e) {
            if (!isRunning) return;
            StopTunnel();
        }

        private void StartTunnel() {
            try {
                string serviceType = cmbServiceType.SelectedItem?.ToString().ToLower() ?? "http";
                string port = txtPort.Text;

                // Arguments 구성 (기본)
                string args = $"tunnel --url {serviceType}://localhost:{port}";

                // 로그 레벨 추가 (없음이 아닐 때만)
                if (cmbLogLevel.SelectedIndex > 0) {
                    string logLevel = cmbLogLevel.SelectedItem?.ToString();
                    args += $" --loglevel {logLevel}";
                }

                // 프로토콜 추가 (없음이 아닐 때만)
                if (cmbProtocol.SelectedIndex > 0) {
                    string protocolType = cmbProtocol.SelectedItem?.ToString();
                    args += $" --protocol {protocolType}";
                }

                // HTTPS 자체 서명 인증서 (서비스 타입이 HTTPS일 때만)
                if (serviceType == "https" && chkHttps.Checked) {
                    args += " --no-tls-verify";
                }

                // 로그 파일
                if (chkLogFile.Checked) {
                    args += " --logfile cloudflared.log";
                }

                AppendLog($"터널 시작 중...");
                AppendLog($"작업 디렉토리: {workingDirectory}");
                AppendLog($"명령어: cloudflared {args}");

                cloudflaredProcess = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = Tol.cloudflaredPath,
                        Arguments = args,
                        WorkingDirectory = workingDirectory, // 작업 디렉토리 설정
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    }
                };

                // 출력 이벤트 핸들러
                cloudflaredProcess.OutputDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        BeginInvoke(new Action(() => {
                            AppendLog(e.Data);
                            ParseTunnelUrl(e.Data);
                        }));
                    }
                };

                cloudflaredProcess.ErrorDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        BeginInvoke(new Action(() => {
                            // cloudflared는 모든 로그를 stderr로 출력하므로 [ERROR] 태그 제거
                            AppendLog(e.Data);
                            ParseTunnelUrl(e.Data);
                        }));
                    }
                };

                // 프로세스 종료 이벤트
                cloudflaredProcess.EnableRaisingEvents = true;
                cloudflaredProcess.Exited += (s, e) => {
                    BeginInvoke(new Action(() => {
                        AppendLog("터널이 종료되었습니다.");
                        UpdateUIState(false);
                    }));
                };

                cloudflaredProcess.Start();
                cloudflaredProcess.BeginOutputReadLine();
                cloudflaredProcess.BeginErrorReadLine();

                isRunning = true;
                UpdateUIState(true);

            } catch (Exception ex) {
                Tol.ShowError($"터널 시작 실패: {ex.Message}");
                AppendLog($"[ERROR] {ex.Message}");
            }
        }

        private void StopTunnel() {
            try {
                if (cloudflaredProcess != null && !cloudflaredProcess.HasExited) {
                    AppendLog("터널을 중지하는 중...");
                    cloudflaredProcess.Kill();
                    cloudflaredProcess.WaitForExit(3000);
                    cloudflaredProcess.Dispose();
                    cloudflaredProcess = null;
                }

                isRunning = false;
                currentTunnelUrl = string.Empty;
                UpdateUIState(false);
                AppendLog("터널이 중지되었습니다.");

            } catch (Exception ex) {
                Tol.ShowError($"터널 중지 실패: {ex.Message}");
            }
        }

        private void ParseTunnelUrl(string logLine) {
            // cloudflared 로그에서 URL 추출
            // 형식: "2026-01-24T14:55:07Z INF |  https://includes-enrolled-lounge-increasingly.trycloudflare.com  |"
            // 또는: "https://random-name.trycloudflare.com"

            // trycloudflare.com URL만 추출 (더 유연한 패턴)
            var match = Regex.Match(logLine, @"https://[\w\-]+\.trycloudflare\.com");

            if (match.Success && string.IsNullOrEmpty(currentTunnelUrl)) {
                currentTunnelUrl = match.Value.Trim();

                // UI 스레드에서 실행
                if (txtStatus.InvokeRequired) {
                    txtStatus.Invoke(new Action(() => {
                        txtStatus.Text = currentTunnelUrl;
                        txtStatus.ForeColor = Color.FromArgb(76, 175, 80);
                    }));
                } else {
                    txtStatus.Text = currentTunnelUrl;
                    txtStatus.ForeColor = Color.FromArgb(76, 175, 80);
                }

                AppendLog($"✅ 터널 URL: {currentTunnelUrl}");

                // 클립보드에 복사
                try {
                    if (InvokeRequired) {
                        Invoke(new Action(() => Clipboard.SetText(currentTunnelUrl)));
                    } else {
                        Clipboard.SetText(currentTunnelUrl);
                    }
                    AppendLog("📋 URL이 클립보드에 복사되었습니다.");
                } catch {
                    AppendLog("⚠️ 클립보드 복사 실패");
                }
            }
        }

        private void UpdateUIState(bool running) {
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            txtPort.Enabled = !running;
            cmbServiceType.Enabled = !running;
            cmbLogLevel.Enabled = !running;
            cmbProtocol.Enabled = !running;
            chkHttps.Enabled = !running;
            chkLogFile.Enabled = !running;
            btnBrowseWorkDir.Enabled = !running;

            if (running) {
                txtStatus.Text = "연결 중... URL 생성 대기";
                txtStatus.ForeColor = Color.FromArgb(255, 152, 0);
            } else {
                txtStatus.Text = "대기 중...";
                txtStatus.ForeColor = Color.FromArgb(100, 100, 100);
            }
        }

        private void AppendLog(string message) {
            if (txtLog.InvokeRequired) {
                txtLog.Invoke(new Action(() => AppendLog(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}\r\n");
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            if (isRunning) {
                var result = Tol.ShowQuestion("터널이 실행 중입니다. 종료하시겠습니까?");
                if (result == DialogResult.Yes) {
                    StopTunnel();
                } else {
                    e.Cancel = true;
                }
            }
        }

        private void btnCopyUrl_Click(object sender, EventArgs e) {
            if (!string.IsNullOrEmpty(currentTunnelUrl)) {
                try {
                    Clipboard.SetText(currentTunnelUrl);
                    Tol.ShowInfo("URL이 클립보드에 복사되었습니다.");
                } catch (Exception ex) {
                    Tol.ShowError($"복사 실패: {ex.Message}");
                }
            } else {
                Tol.ShowWarning("복사할 URL이 없습니다.");
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e) {
            txtLog.Clear();
            AppendLog("로그가 초기화되었습니다.");
        }

        private void btnBrowseWorkDir_Click(object sender, EventArgs e) {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
                dialog.Description = "작업 디렉토리를 선택하세요";
                dialog.SelectedPath = workingDirectory;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK) {
                    workingDirectory = dialog.SelectedPath;
                    txtWorkingDirectory.Text = workingDirectory;
                    AppendLog($"작업 디렉토리 변경됨: {workingDirectory}");
                }
            }
        }

        private void cmbServiceType_SelectedIndexChanged(object sender, EventArgs e) {
            // 서비스 타입에 따라 기본 포트 자동 설정
            string serviceType = cmbServiceType.SelectedItem?.ToString();

            switch (serviceType) {
                case "HTTP":
                    if (txtPort.Text == "443" || txtPort.Text == "25565") {
                        txtPort.Text = "8000";
                    }
                    chkHttps.Enabled = false;
                    chkHttps.Checked = false;
                    break;

                case "HTTPS":
                    if (txtPort.Text == "8000" || txtPort.Text == "25565") {
                        txtPort.Text = "443";
                    }
                    chkHttps.Enabled = true;
                    break;

                case "TCP":
                    if (txtPort.Text == "8000" || txtPort.Text == "443") {
                        txtPort.Text = "25565"; // 마인크래프트 기본 포트
                    }
                    chkHttps.Enabled = false;
                    chkHttps.Checked = false;
                    break;
            }
        }
    }
}