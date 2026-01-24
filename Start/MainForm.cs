// MainForm.cs - 로직 코드 (수정됨)
using CloudflaredTunnelHost.Tools;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CloudflaredTunnelHost.Start {
    public partial class MainForm : DevForm {
        private Process cloudflaredProcess;
        private bool isRunning = false;
        private string currentTunnelUrl = string.Empty;
        private DateTime tunnelStartTime;
        private System.Windows.Forms.Timer uptimeTimer;
        private NotifyIcon trayIcon;

        public MainForm() {
            InitializeComponent();
            InitializeTimer();
            InitializeTrayIcon();
        }

        private void MainForm_Load(object sender, EventArgs e) {
            this.Text = "Cloudflared Tunnel Host";

            // 콤보박스 초기값 설정
            cmbServiceType.SelectedIndex = 0; // HTTP
            cmbLogLevel.SelectedIndex = 0; // 없음
            cmbProtocol.SelectedIndex = 0; // 없음

            // 저장된 설정 불러오기
            LoadSettings();

            AppendLog("프로그램이 시작되었습니다.");
        }

        private void InitializeTimer() {
            uptimeTimer = new System.Windows.Forms.Timer();
            uptimeTimer.Interval = 1000; // 1초마다
            uptimeTimer.Tick += UptimeTimer_Tick;
        }

        private void InitializeTrayIcon() {
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Cloudflared Tunnel Host";
            trayIcon.Icon = this.Icon ?? SystemIcons.Application;
            trayIcon.DoubleClick += (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("열기", null, (s, e) => {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            });
            contextMenu.Items.Add("URL 복사", null, (s, e) => {
                if (!string.IsNullOrEmpty(currentTunnelUrl)) {
                    Clipboard.SetText(currentTunnelUrl);
                }
            });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("종료", null, (s, e) => {
                ProgramExit();
            });
            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void UptimeTimer_Tick(object sender, EventArgs e) {
            if (isRunning) {
                TimeSpan uptime = DateTime.Now - tunnelStartTime;
                lblUptime.Text = $"{uptime:hh\\:mm\\:ss}";

                // 타이틀에 실행 시간 표시
                this.Text = $"🟢 Cloudflared Tunnel Host - 실행 중 ({uptime:hh\\:mm\\:ss})";
            }
        }

        private void LoadSettings() {
            try {
                string settingsFile = Path.Combine(Tol.dataPath, "settings.txt");
                if (File.Exists(settingsFile)) {
                    var lines = File.ReadAllLines(settingsFile);
                    foreach (var line in lines) {
                        var parts = line.Split('=');
                        if (parts.Length == 2) {
                            switch (parts[0].Trim()) {
                                case "Port":
                                    txtPort.Text = parts[1].Trim();
                                    break;
                                case "ServiceType":
                                    var index = cmbServiceType.FindString(parts[1].Trim());
                                    if (index >= 0) cmbServiceType.SelectedIndex = index;
                                    break;
                                case "LogLevel":
                                    index = cmbLogLevel.FindString(parts[1].Trim());
                                    if (index >= 0) cmbLogLevel.SelectedIndex = index;
                                    break;
                                case "Protocol":
                                    index = cmbProtocol.FindString(parts[1].Trim());
                                    if (index >= 0) cmbProtocol.SelectedIndex = index;
                                    break;
                                case "HttpsNoVerify":
                                    chkHttps.Checked = parts[1].Trim() == "True";
                                    break;
                                case "LogFile":
                                    chkLogFile.Checked = parts[1].Trim() == "True";
                                    break;
                            }
                        }
                    }
                    AppendLog("이전 설정을 불러왔습니다.");
                }
            } catch (Exception ex) {
                AppendLog($"설정 불러오기 실패: {ex.Message}");
            }
        }

        private void SaveSettings() {
            try {
                string settingsFile = Path.Combine(Tol.dataPath, "settings.txt");
                var settings = new List<string> {
                    $"Port={txtPort.Text}",
                    $"ServiceType={cmbServiceType.SelectedItem}",
                    $"LogLevel={cmbLogLevel.SelectedItem}",
                    $"Protocol={cmbProtocol.SelectedItem}",
                    $"HttpsNoVerify={chkHttps.Checked}",
                    $"LogFile={chkLogFile.Checked}"
                };
                File.WriteAllLines(settingsFile, settings);
                AppendLog("설정이 저장되었습니다.");
            } catch (Exception ex) {
                AppendLog($"설정 저장 실패: {ex.Message}");
            }
        }

        private void MainForm_Shown(object sender, EventArgs e) {
            if (!File.Exists(Tol.cloudflaredPath)) {
                if (Tol.ShowQuestion("""
                    해당 프로그램을 이용하기 위해 Cloudflared Tunnel이 필요합니다.
                    다운로드 하시겠습니까?

                    파일은 공식 GitHub 릴리즈 페이지에서 제공하는 파일을 사용합니다.

                    취소할 경우 프로그램이 종료됩니다.
                    """) == DialogResult.Yes) {
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
                    args += $" --logfile \"{Path.Combine(Tol.dataPath, "cloudflared.log")}\"";
                }

                AppendLog($"터널 시작 중...");
                AppendLog($"명령어: cloudflared {args}");

                cloudflaredProcess = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = Tol.cloudflaredPath,
                        Arguments = args,
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
                        currentTunnelUrl = string.Empty;
                        uptimeTimer.Stop();
                    }));
                };

                cloudflaredProcess.Start();
                cloudflaredProcess.BeginOutputReadLine();
                cloudflaredProcess.BeginErrorReadLine();

                isRunning = true;
                tunnelStartTime = DateTime.Now;
                uptimeTimer.Start();
                UpdateUIState(true);

                // 설정 저장
                SaveSettings();

            } catch (Exception ex) {
                Tol.ShowError($"터널 시작 실패: {ex.Message}");
                AppendLog($"[오류] {ex.Message}");
            }
        }

        private void StopTunnel() {
            try {
                if (cloudflaredProcess != null && !cloudflaredProcess.HasExited) {
                    AppendLog("터널을 중지하는 중...");

                    // 출력 스트림 먼저 취소
                    cloudflaredProcess.CancelOutputRead();
                    cloudflaredProcess.CancelErrorRead();

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
                AppendLog($"[오류] 터널 중지 실패: {ex.Message}");
            }
        }

        private void ParseTunnelUrl(string logLine) {
            // cloudflared 로그에서 URL 추출
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

                // 트레이 아이콘 알림
                trayIcon.ShowBalloonTip(3000, "터널 시작됨",
                    $"URL: {currentTunnelUrl}", ToolTipIcon.Info);

                // 클립보드에 복사
                try {
                    if (InvokeRequired) {
                        Invoke(new Action(() => {
                            try {
                                Clipboard.SetText(currentTunnelUrl);
                                AppendLog("📋 URL이 클립보드에 복사되었습니다.");
                            } catch {
                                AppendLog("⚠️ 클립보드 복사 실패");
                            }
                        }));
                    } else {
                        Clipboard.SetText(currentTunnelUrl);
                        AppendLog("📋 URL이 클립보드에 복사되었습니다.");
                    }
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
            chkHttps.Enabled = !running && cmbServiceType.SelectedItem?.ToString() == "HTTPS";
            chkLogFile.Enabled = !running;
            btnOpenBrowser.Enabled = running;
            btnQRCode.Enabled = running;
            btnCopyUrl.Enabled = running;

            if (running) {
                txtStatus.Text = "연결 중... URL 생성 대기";
                txtStatus.ForeColor = Color.FromArgb(255, 152, 0);
                lblUptime.ForeColor = Color.FromArgb(76, 175, 80);
                this.Text = "🟢 Cloudflared Tunnel Host - 실행 중";
            } else {
                txtStatus.Text = "대기 중...";
                txtStatus.ForeColor = Color.FromArgb(100, 100, 100);
                lblUptime.Text = "00:00:00";
                lblUptime.ForeColor = SystemColors.WindowFrame;
                this.Text = "⚫ Cloudflared Tunnel Host - 대기 중";
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
            ProgramExit(e);
        }

        private void ProgramExit(FormClosingEventArgs e = null) {
            if (isRunning) {
                var result = Tol.ShowQuestion("터널이 실행 중입니다. 종료하시겠습니까?");
                if (result == DialogResult.Yes) {
                    StopTunnel();
                    trayIcon.Visible = false;
                } else {
                    if (e != null) e.Cancel = true;
                }
            } else {
                trayIcon.Visible = false;
            }
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);

            // 최소화 시 트레이로
            if (WindowState == FormWindowState.Minimized) {
                this.Hide();
                this.ShowInTaskbar = false;
                trayIcon.Visible = true;
                trayIcon.ShowBalloonTip(1000, "Cloudflared Tunnel Host",
                    "프로그램이 트레이로 최소화되었습니다.", ToolTipIcon.Info);
            }
        }

        private void btnOpenBrowser_Click(object sender, EventArgs e) {
            if (!string.IsNullOrEmpty(currentTunnelUrl)) {
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = currentTunnelUrl,
                        UseShellExecute = true
                    });
                    AppendLog("브라우저에서 URL을 열었습니다.");
                } catch (Exception ex) {
                    Tol.ShowError($"브라우저 열기 실패: {ex.Message}");
                }
            } else {
                Tol.ShowWarning("열 URL이 없습니다.");
            }
        }

        private void btnSaveLog_Click(object sender, EventArgs e) {
            try {
                using (SaveFileDialog sfd = new SaveFileDialog()) {
                    sfd.Filter = "텍스트 파일|*.txt|모든 파일|*.*";
                    sfd.FileName = $"tunnel_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                    if (sfd.ShowDialog() == DialogResult.OK) {
                        File.WriteAllText(sfd.FileName, txtLog.Text);
                        Tol.ShowInfo("로그가 저장되었습니다.");
                        AppendLog($"로그 저장: {sfd.FileName}");
                    }
                }
            } catch (Exception ex) {
                Tol.ShowError($"로그 저장 실패: {ex.Message}");
            }
        }

        private void btnQRCode_Click(object sender, EventArgs e) {
            if (!string.IsNullOrEmpty(currentTunnelUrl)) {
                new QRViewer(currentTunnelUrl).Show();
            } else {
                Tol.ShowWarning("QR 코드로 만들 URL이 없습니다.");
            }
        }

        private void btnViewLogFile_Click(object sender, EventArgs e) {
            string logFilePath = Path.Combine(Tol.dataPath, "cloudflared.log");
            if (File.Exists(logFilePath)) {
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = logFilePath,
                        UseShellExecute = true
                    });
                } catch (Exception ex) {
                    Tol.ShowError($"로그 파일 열기 실패: {ex.Message}");
                }
            } else {
                Tol.ShowWarning("로그 파일이 존재하지 않습니다.");
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
                    chkHttps.Enabled = !isRunning;
                    break;
            }
        }
    }
}