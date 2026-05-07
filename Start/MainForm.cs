// MainForm.cs - 개선된 버전
using CloudflaredTunnelHost.Tools;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CloudflaredTunnelHost.Start {
    public partial class MainForm : DevForm {
        private Process? cloudflaredProcess;
        private bool isRunning = false;
        private string currentTunnelUrl = string.Empty;
        private DateTime tunnelStartTime;
        private System.Windows.Forms.Timer? uptimeTimer;
        private NotifyIcon? trayIcon;
        private bool isDisposed = false;

        private const int MaxLogLines = 2000;

        private static readonly Regex TunnelUrlRegex =
            new Regex(@"https://[\w\-]+\.trycloudflare\.com", RegexOptions.Compiled);

        // ── 트레이 메뉴 아이템 참조 (상태에 따라 직접 접근) ──────────────────
        private ToolStripMenuItem? _trayMenuStart;
        private ToolStripMenuItem? _trayMenuStop;
        private ToolStripMenuItem? _trayMenuStatus;
        private ToolStripMenuItem? _trayMenuUptime;
        private ToolStripMenuItem? _trayMenuCopyUrl;
        private ToolStripMenuItem? _trayMenuOpenBrowser;
        private ToolStripMenuItem? _trayMenuQR;
        private ToolStripMenuItem? _trayMenuLogFile;

        public MainForm() {
            InitializeComponent();
            InitializeTimer();
            InitializeTrayIcon();
            this.FormClosed += MainForm_FormClosed;
        }

        private void MainForm_Load(object sender, EventArgs e) {
            this.Text = "Cloudflared Tunnel Host";
            cmbServiceType.SelectedIndex = 0;
            cmbLogLevel.SelectedIndex = 0;
            cmbProtocol.SelectedIndex = 0;
            LoadSettings();
            AppendLog("프로그램이 시작되었습니다.");
        }

        private void InitializeTimer() {
            uptimeTimer = new System.Windows.Forms.Timer();
            uptimeTimer.Interval = 1000;
            uptimeTimer.Tick += UptimeTimer_Tick;
        }

        private void InitializeTrayIcon() {
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Cloudflared Tunnel Host";
            trayIcon.Icon = this.Icon ?? SystemIcons.Application;
            trayIcon.Visible = false;
            trayIcon.DoubleClick += (s, e) => RestoreWindow();

            var menu = new ContextMenuStrip();
            menu.Opening += TrayMenu_Opening;

            // ── 헤더: 상태 표시 (클릭 불가) ──────────────────────────────
            _trayMenuStatus = new ToolStripMenuItem("⚫ 대기 중") {
                Enabled = false,
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            menu.Items.Add(_trayMenuStatus);

            // ── 가동 시간 표시 (클릭 불가) ──────────────────────────────
            _trayMenuUptime = new ToolStripMenuItem("⏱ 가동시간: --:--:--") {
                Enabled = false
            };
            menu.Items.Add(_trayMenuUptime);

            menu.Items.Add(new ToolStripSeparator());

            // ── 터널 제어 ────────────────────────────────────────────────
            _trayMenuStart = new ToolStripMenuItem("▶ 터널 시작");
            _trayMenuStart.Click += (s, e) => {
                RestoreWindow();
                if (!isRunning) btnStart_Click(s!, EventArgs.Empty);
            };
            menu.Items.Add(_trayMenuStart);

            _trayMenuStop = new ToolStripMenuItem("■ 터널 중지") { Enabled = false };
            _trayMenuStop.Click += (s, e) => StopTunnel();
            menu.Items.Add(_trayMenuStop);

            menu.Items.Add(new ToolStripSeparator());

            // ── URL 관련 ─────────────────────────────────────────────────
            _trayMenuCopyUrl = new ToolStripMenuItem("📋 URL 복사") { Enabled = false };
            _trayMenuCopyUrl.Click += (s, e) => {
                if (!string.IsNullOrEmpty(currentTunnelUrl)) {
                    try {
                        Clipboard.SetText(currentTunnelUrl);
                        trayIcon?.ShowBalloonTip(1500, "복사됨", currentTunnelUrl, ToolTipIcon.Info);
                    } catch { }
                }
            };
            menu.Items.Add(_trayMenuCopyUrl);

            _trayMenuOpenBrowser = new ToolStripMenuItem("🌐 브라우저로 열기") { Enabled = false };
            _trayMenuOpenBrowser.Click += (s, e) => {
                if (string.IsNullOrEmpty(currentTunnelUrl)) return;
                try {
                    Process.Start(new ProcessStartInfo {
                        FileName = currentTunnelUrl,
                        UseShellExecute = true
                    });
                } catch (Exception ex) {
                    Tol.ShowError($"브라우저 열기 실패: {ex.Message}");
                }
            };
            menu.Items.Add(_trayMenuOpenBrowser);

            _trayMenuQR = new ToolStripMenuItem("📷 QR 코드 보기") { Enabled = false };
            _trayMenuQR.Click += (s, e) => {
                if (!string.IsNullOrEmpty(currentTunnelUrl))
                    new QRViewer(currentTunnelUrl, Uri.EscapeDataString(currentTunnelUrl)).Show();
            };
            menu.Items.Add(_trayMenuQR);

            menu.Items.Add(new ToolStripSeparator());

            // ── 로그 ─────────────────────────────────────────────────────
            _trayMenuLogFile = new ToolStripMenuItem("📄 로그 파일 열기");
            _trayMenuLogFile.Click += (s, e) => {
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
                    string hint = chkLogFile.Checked ? "" : "\n\n'로그 파일 저장' 옵션이 비활성화되어 있습니다.";
                    Tol.ShowWarning($"로그 파일이 존재하지 않습니다.{hint}");
                }
            };
            menu.Items.Add(_trayMenuLogFile);

            menu.Items.Add(new ToolStripSeparator());

            // ── 창 제어 ──────────────────────────────────────────────────
            var menuRestore = new ToolStripMenuItem("🪟 창 열기");
            menuRestore.Click += (s, e) => RestoreWindow();
            menu.Items.Add(menuRestore);

            menu.Items.Add(new ToolStripSeparator());

            // ── 종료 ─────────────────────────────────────────────────────
            var menuExit = new ToolStripMenuItem("✖ 종료");
            menuExit.Click += (s, e) => ProgramExit();
            menu.Items.Add(menuExit);

            trayIcon.ContextMenuStrip = menu;
        }

        /// <summary>트레이 메뉴가 열릴 때마다 현재 상태를 반영합니다.</summary>
        private void TrayMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e) {
            if (_trayMenuStatus == null) return;

            bool hasUrl = !string.IsNullOrEmpty(currentTunnelUrl);

            // 상태 헤더
            _trayMenuStatus.Text = isRunning
                ? (hasUrl ? $"🟢 실행 중  |  {currentTunnelUrl}" : "🟡 연결 중...")
                : "⚫ 대기 중";

            // 가동시간
            if (isRunning) {
                TimeSpan uptime = DateTime.Now - tunnelStartTime;
                if (_trayMenuUptime != null) {
                    _trayMenuUptime.Text = $"⏱ 가동시간: {uptime:hh\\:mm\\:ss}";
                    _trayMenuUptime.Visible = true;
                }
            } else {
                if (_trayMenuUptime != null) _trayMenuUptime.Visible = false;
            }

            // 터널 제어
            if (_trayMenuStart != null) _trayMenuStart.Enabled = !isRunning;
            if (_trayMenuStop != null) _trayMenuStop.Enabled = isRunning;

            // URL 관련 (URL 확정 후에만 활성화)
            if (_trayMenuCopyUrl != null) _trayMenuCopyUrl.Enabled = hasUrl;
            if (_trayMenuOpenBrowser != null) _trayMenuOpenBrowser.Enabled = hasUrl;
            if (_trayMenuQR != null) _trayMenuQR.Enabled = hasUrl;

            // 로그 파일 존재 여부
            if (_trayMenuLogFile != null) {
                bool logExists = File.Exists(Path.Combine(Tol.dataPath, "cloudflared.log"));
                _trayMenuLogFile.Enabled = logExists;
                _trayMenuLogFile.Text = logExists ? "📄 로그 파일 열기" : "📄 로그 파일 없음";
            }
        }

        /// <summary>트레이에서 창을 복원합니다.</summary>
        private void RestoreWindow() {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            if (trayIcon != null) trayIcon.Visible = false;
            this.Activate();
        }

        private void UptimeTimer_Tick(object? sender, EventArgs e) {
            if (!isRunning) return;
            TimeSpan uptime = DateTime.Now - tunnelStartTime;
            lblUptime.Text = $"{uptime:hh\\:mm\\:ss}";
            this.Text = $"🟢 Cloudflared Tunnel Host - 실행 중 ({uptime:hh\\:mm\\:ss})";

            // 트레이 툴팁 실시간 갱신 (최대 63자)
            if (trayIcon != null && trayIcon.Visible) {
                string urlShort = currentTunnelUrl.Length > 40
                    ? currentTunnelUrl[..40] + "…" : currentTunnelUrl;
                string tooltip = string.IsNullOrEmpty(urlShort)
                    ? $"🟢 실행 중 ({uptime:hh\\:mm\\:ss})"
                    : $"🟢 {uptime:hh\\:mm\\:ss}  {urlShort}";
                trayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
            }
        }

        private void LoadSettings() {
            try {
                string settingsFile = Path.Combine(Tol.dataPath, "settings.txt");
                if (!File.Exists(settingsFile)) return;
                var lines = File.ReadAllLines(settingsFile);
                foreach (var line in lines) {
                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    switch (key) {
                        case "Port":
                            if (int.TryParse(value, out int savedPort) && savedPort >= 1 && savedPort <= 65535)
                                txtPort.Text = value;
                            break;
                        case "ServiceType":
                            var idx = cmbServiceType.FindString(value);
                            if (idx >= 0) cmbServiceType.SelectedIndex = idx;
                            break;
                        case "LogLevel":
                            idx = cmbLogLevel.FindString(value);
                            if (idx >= 0) cmbLogLevel.SelectedIndex = idx;
                            break;
                        case "Protocol":
                            idx = cmbProtocol.FindString(value);
                            if (idx >= 0) cmbProtocol.SelectedIndex = idx;
                            break;
                        case "HttpsNoVerify":
                            chkHttps.Checked = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "LogFile":
                            chkLogFile.Checked = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
                AppendLog("이전 설정을 불러왔습니다.");
            } catch (Exception ex) {
                AppendLog($"설정 불러오기 실패: {ex.Message}");
            }
        }

        private void SaveSettings() {
            try {
                if (!Directory.Exists(Tol.dataPath))
                    Directory.CreateDirectory(Tol.dataPath);
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
                    using var dlg = new Download();
                    if (dlg.ShowDialog() != DialogResult.OK)
                        Application.Exit();
                } else {
                    Application.Exit();
                }
            } else {
                AppendLog($"cloudflared.exe 경로: {Tol.cloudflaredPath}");
            }
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) {
                this.Hide();
                this.ShowInTaskbar = false;
                if (trayIcon != null) {
                    trayIcon.Visible = true;
                    string msg = isRunning
                        ? $"터널 실행 중\n{currentTunnelUrl}"
                        : "트레이로 최소화되었습니다.";
                    trayIcon.ShowBalloonTip(1500, "Cloudflared Tunnel Host", msg, ToolTipIcon.Info);
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            ProgramExit(e);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e) {
            isDisposed = true;
            uptimeTimer?.Stop();
            uptimeTimer?.Dispose();
            uptimeTimer = null;
            if (cloudflaredProcess != null) {
                try {
                    if (!cloudflaredProcess.HasExited) {
                        cloudflaredProcess.Kill();
                        cloudflaredProcess.WaitForExit(3000);
                    }
                } catch { }
                cloudflaredProcess.Dispose();
                cloudflaredProcess = null;
            }
            if (trayIcon != null) {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private void ProgramExit(FormClosingEventArgs? e = null) {
            if (isRunning) {
                var result = Tol.ShowQuestion("터널이 실행 중입니다. 종료하시겠습니까?");
                if (result == DialogResult.Yes) {
                    StopTunnel();
                } else {
                    if (e != null) e.Cancel = true;
                }
            }
        }

        private void btnStart_Click(object sender, EventArgs e) {
            if (isRunning) return;
            if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535) {
                Tol.ShowError("올바른 포트 번호를 입력해주세요 (1-65535)");
                return;
            }
            if (!File.Exists(Tol.cloudflaredPath)) {
                Tol.ShowError("cloudflared.exe를 찾을 수 없습니다. 프로그램을 재시작하거나 수동으로 다운로드하세요.");
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
                string serviceType = cmbServiceType.SelectedItem?.ToString()?.ToLower() ?? "http";
                string port = txtPort.Text.Trim();
                string args = $"tunnel --url {serviceType}://localhost:{port}";

                if (cmbLogLevel.SelectedIndex > 0) {
                    string logLevel = cmbLogLevel.SelectedItem?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(logLevel)) args += $" --loglevel {logLevel}";
                }
                if (cmbProtocol.SelectedIndex > 0) {
                    string protocolType = cmbProtocol.SelectedItem?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(protocolType)) args += $" --protocol {protocolType}";
                }
                if (serviceType == "https" && chkHttps.Checked) args += " --no-tls-verify";
                if (chkLogFile.Checked)
                    args += $" --logfile \"{Path.Combine(Tol.dataPath, "cloudflared.log")}\"";

                AppendLog("터널 시작 중...");
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
                    },
                    EnableRaisingEvents = true
                };

                cloudflaredProcess.OutputDataReceived += OnProcessDataReceived;
                cloudflaredProcess.ErrorDataReceived += OnProcessDataReceived;

                cloudflaredProcess.Exited += (s, ev) => {
                    if (isDisposed || !this.IsHandleCreated) return;
                    BeginInvoke(new Action(() => {
                        int exitCode = -1;
                        try { exitCode = cloudflaredProcess?.ExitCode ?? -1; } catch { }
                        if (isRunning)
                            AppendLog($"⚠️ 터널이 예기치 않게 종료되었습니다. (종료 코드: {exitCode})");
                        else
                            AppendLog("터널이 종료되었습니다.");
                        isRunning = false;
                        currentTunnelUrl = string.Empty;
                        uptimeTimer?.Stop();
                        UpdateUIState(false);
                    }));
                };

                if (!cloudflaredProcess.Start())
                    throw new InvalidOperationException("프로세스를 시작할 수 없습니다.");

                cloudflaredProcess.BeginOutputReadLine();
                cloudflaredProcess.BeginErrorReadLine();

                isRunning = true;
                tunnelStartTime = DateTime.Now;
                uptimeTimer?.Start();
                UpdateUIState(true);
                SaveSettings();

            } catch (Exception ex) {
                isRunning = false;
                cloudflaredProcess?.Dispose();
                cloudflaredProcess = null;
                UpdateUIState(false);
                Tol.ShowError($"터널 시작 실패: {ex.Message}");
                AppendLog($"[오류] {ex.Message}");
            }
        }

        private void StopTunnel() {
            isRunning = false;
            try {
                if (cloudflaredProcess != null && !cloudflaredProcess.HasExited) {
                    AppendLog("터널을 중지하는 중...");
                    try { cloudflaredProcess.CancelOutputRead(); } catch { }
                    try { cloudflaredProcess.CancelErrorRead(); } catch { }
                    cloudflaredProcess.Kill();
                    if (!cloudflaredProcess.WaitForExit(5000))
                        AppendLog("⚠️ 프로세스 강제 종료 타임아웃");
                }
            } catch (Exception ex) {
                AppendLog($"[오류] 프로세스 종료 중 오류: {ex.Message}");
            } finally {
                cloudflaredProcess?.Dispose();
                cloudflaredProcess = null;
                currentTunnelUrl = string.Empty;
                uptimeTimer?.Stop();
                UpdateUIState(false);
                AppendLog("터널이 중지되었습니다.");
            }
        }

        private void OnProcessDataReceived(object sender, DataReceivedEventArgs e) {
            if (string.IsNullOrEmpty(e.Data)) return;
            if (isDisposed || !this.IsHandleCreated) return;
            BeginInvoke(new Action(() => {
                AppendLog(e.Data);
                ParseTunnelUrl(e.Data);
            }));
        }

        private void ParseTunnelUrl(string logLine) {
            if (!string.IsNullOrEmpty(currentTunnelUrl)) return;
            var match = TunnelUrlRegex.Match(logLine);
            if (!match.Success) return;

            currentTunnelUrl = match.Value.Trim();
            txtStatus.Text = currentTunnelUrl;
            txtStatus.ForeColor = Color.FromArgb(76, 175, 80);
            AppendLog($"✅ 터널 URL: {currentTunnelUrl}");

            if (trayIcon != null) {
                trayIcon.ShowBalloonTip(4000, "🟢 터널 연결됨", currentTunnelUrl, ToolTipIcon.Info);
                string tooltip = $"🟢 실행 중\n{currentTunnelUrl}";
                trayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
            }

            try {
                Clipboard.SetText(currentTunnelUrl);
                AppendLog("📋 URL이 클립보드에 복사되었습니다.");
            } catch {
                AppendLog("⚠️ 클립보드 복사 실패");
            }

            // URL 확정 → URL 관련 트레이 메뉴 즉시 활성화
            UpdateTrayUrlItems(true);
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
                if (trayIcon != null) trayIcon.Text = "Cloudflared Tunnel Host";
            }

            // 트레이 제어 메뉴 동기화
            if (_trayMenuStart != null) _trayMenuStart.Enabled = !running;
            if (_trayMenuStop != null) _trayMenuStop.Enabled = running;
            if (!running) UpdateTrayUrlItems(false);
        }

        /// <summary>URL 확정/초기화에 따라 트레이 URL 관련 항목을 일괄 갱신합니다.</summary>
        private void UpdateTrayUrlItems(bool enable) {
            if (_trayMenuCopyUrl != null) _trayMenuCopyUrl.Enabled = enable;
            if (_trayMenuOpenBrowser != null) _trayMenuOpenBrowser.Enabled = enable;
            if (_trayMenuQR != null) _trayMenuQR.Enabled = enable;
        }

        private void AppendLog(string message) {
            if (isDisposed) return;
            if (txtLog.InvokeRequired) {
                try { txtLog.Invoke(new Action(() => AppendLog(message))); } catch (ObjectDisposedException) { }
                return;
            }
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}\r\n");
            if (txtLog.Lines.Length > MaxLogLines) {
                var lines = txtLog.Lines.Skip(txtLog.Lines.Length - MaxLogLines).ToArray();
                txtLog.Lines = lines;
            }
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void btnOpenBrowser_Click(object sender, EventArgs e) {
            if (string.IsNullOrEmpty(currentTunnelUrl)) { Tol.ShowWarning("열 URL이 없습니다."); return; }
            try {
                if (!Uri.TryCreate(currentTunnelUrl, UriKind.Absolute, out _)) {
                    Tol.ShowWarning("유효하지 않은 URL입니다."); return;
                }
                Process.Start(new ProcessStartInfo { FileName = currentTunnelUrl, UseShellExecute = true });
                AppendLog("브라우저에서 URL을 열었습니다.");
            } catch (Exception ex) {
                Tol.ShowError($"브라우저 열기 실패: {ex.Message}");
            }
        }

        private void btnSaveLog_Click(object sender, EventArgs e) {
            try {
                using var sfd = new SaveFileDialog {
                    Filter = "텍스트 파일|*.txt|모든 파일|*.*",
                    FileName = $"tunnel_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };
                if (sfd.ShowDialog() == DialogResult.OK) {
                    File.WriteAllText(sfd.FileName, txtLog.Text, System.Text.Encoding.UTF8);
                    Tol.ShowInfo("로그가 저장되었습니다.");
                    AppendLog($"로그 저장: {sfd.FileName}");
                }
            } catch (Exception ex) {
                Tol.ShowError($"로그 저장 실패: {ex.Message}");
            }
        }

        private void btnQRCode_Click(object sender, EventArgs e) {
            if (string.IsNullOrEmpty(currentTunnelUrl)) { Tol.ShowWarning("QR 코드로 만들 URL이 없습니다."); return; }
            new QRViewer(currentTunnelUrl, Uri.EscapeDataString(currentTunnelUrl)).Show();
        }

        private void btnViewLogFile_Click(object sender, EventArgs e) {
            string logFilePath = Path.Combine(Tol.dataPath, "cloudflared.log");
            if (File.Exists(logFilePath)) {
                try {
                    Process.Start(new ProcessStartInfo { FileName = logFilePath, UseShellExecute = true });
                } catch (Exception ex) {
                    Tol.ShowError($"로그 파일 열기 실패: {ex.Message}");
                }
            } else {
                string hint = chkLogFile.Checked ? "" : "\n\n'로그 파일 저장' 옵션이 비활성화되어 있습니다.";
                Tol.ShowWarning($"로그 파일이 존재하지 않습니다.{hint}");
            }
        }

        private void btnCopyUrl_Click(object sender, EventArgs e) {
            if (string.IsNullOrEmpty(currentTunnelUrl)) { Tol.ShowWarning("복사할 URL이 없습니다."); return; }
            try {
                Clipboard.SetText(currentTunnelUrl);
                AppendLog("📋 URL이 클립보드에 복사되었습니다.");
            } catch (Exception ex) {
                Tol.ShowError($"복사 실패: {ex.Message}");
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e) {
            txtLog.Clear();
            AppendLog("로그가 초기화되었습니다.");
        }

        private void cmbServiceType_SelectedIndexChanged(object sender, EventArgs e) {
            string? serviceType = cmbServiceType.SelectedItem?.ToString();
            switch (serviceType) {
                case "HTTP":
                    if (txtPort.Text == "443" || txtPort.Text == "25565") txtPort.Text = "8000";
                    chkHttps.Enabled = false;
                    chkHttps.Checked = false;
                    break;
                case "HTTPS":
                    if (txtPort.Text == "8000" || txtPort.Text == "25565") txtPort.Text = "443";
                    chkHttps.Enabled = !isRunning;
                    break;
                default:
                    chkHttps.Enabled = false;
                    chkHttps.Checked = false;
                    break;
            }
        }
    }
}