namespace CloudflaredTunnelHost.Start {
    partial class MainForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            btnStart = new Button();
            btnStop = new Button();
            txtLog = new TextBox();
            txtStatus = new TextBox();
            grpSettings = new GroupBox();
            btnBrowseWorkDir = new Button();
            txtWorkingDirectory = new TextBox();
            lblWorkDir = new Label();
            cmbServiceType = new ComboBox();
            lblServiceType = new Label();
            chkLogFile = new CheckBox();
            chkHttps = new CheckBox();
            cmbProtocol = new ComboBox();
            lblProtocol = new Label();
            cmbLogLevel = new ComboBox();
            lblLogLevel = new Label();
            txtPort = new TextBox();
            lblPort = new Label();
            grpStatus = new GroupBox();
            btnCopyUrl = new Button();
            lblUrl = new Label();
            grpLog = new GroupBox();
            btnClearLog = new Button();
            grpSettings.SuspendLayout();
            grpStatus.SuspendLayout();
            grpLog.SuspendLayout();
            SuspendLayout();
            // 
            // btnStart
            // 
            btnStart.BackColor = Color.FromArgb(76, 175, 80);
            btnStart.Cursor = Cursors.Hand;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.Font = new Font("맑은 고딕", 10F, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.Location = new Point(12, 549);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(135, 40);
            btnStart.TabIndex = 0;
            btnStart.Text = "🚀 터널 시작";
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.BackColor = Color.FromArgb(244, 67, 54);
            btnStop.Cursor = Cursors.Hand;
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.FlatStyle = FlatStyle.Flat;
            btnStop.Font = new Font("맑은 고딕", 10F, FontStyle.Bold);
            btnStop.ForeColor = Color.White;
            btnStop.Location = new Point(153, 549);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(135, 40);
            btnStop.TabIndex = 1;
            btnStop.Text = "⏹ 터널 중지";
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Click += btnStop_Click;
            // 
            // txtLog
            // 
            txtLog.BackColor = Color.FromArgb(30, 30, 30);
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ForeColor = Color.FromArgb(200, 200, 200);
            txtLog.Location = new Point(10, 25);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(469, 506);
            txtLog.TabIndex = 2;
            // 
            // txtStatus
            // 
            txtStatus.BackColor = Color.FromArgb(245, 245, 245);
            txtStatus.BorderStyle = BorderStyle.None;
            txtStatus.Font = new Font("맑은 고딕", 11F, FontStyle.Bold);
            txtStatus.ForeColor = Color.FromArgb(100, 100, 100);
            txtStatus.Location = new Point(10, 40);
            txtStatus.Name = "txtStatus";
            txtStatus.ReadOnly = true;
            txtStatus.Size = new Size(260, 20);
            txtStatus.TabIndex = 3;
            txtStatus.Text = "대기 중...";
            txtStatus.TextAlign = HorizontalAlignment.Center;
            // 
            // grpSettings
            // 
            grpSettings.Controls.Add(btnBrowseWorkDir);
            grpSettings.Controls.Add(txtWorkingDirectory);
            grpSettings.Controls.Add(lblWorkDir);
            grpSettings.Controls.Add(cmbServiceType);
            grpSettings.Controls.Add(lblServiceType);
            grpSettings.Controls.Add(chkLogFile);
            grpSettings.Controls.Add(chkHttps);
            grpSettings.Controls.Add(cmbProtocol);
            grpSettings.Controls.Add(lblProtocol);
            grpSettings.Controls.Add(cmbLogLevel);
            grpSettings.Controls.Add(lblLogLevel);
            grpSettings.Controls.Add(txtPort);
            grpSettings.Controls.Add(lblPort);
            grpSettings.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            grpSettings.Location = new Point(12, 12);
            grpSettings.Name = "grpSettings";
            grpSettings.Size = new Size(276, 288);
            grpSettings.TabIndex = 4;
            grpSettings.TabStop = false;
            grpSettings.Text = "⚙️ 설정";
            // 
            // btnBrowseWorkDir
            // 
            btnBrowseWorkDir.BackColor = Color.FromArgb(96, 125, 139);
            btnBrowseWorkDir.Cursor = Cursors.Hand;
            btnBrowseWorkDir.FlatAppearance.BorderSize = 0;
            btnBrowseWorkDir.FlatStyle = FlatStyle.Flat;
            btnBrowseWorkDir.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnBrowseWorkDir.ForeColor = Color.White;
            btnBrowseWorkDir.Location = new Point(216, 169);
            btnBrowseWorkDir.Name = "btnBrowseWorkDir";
            btnBrowseWorkDir.Size = new Size(44, 23);
            btnBrowseWorkDir.TabIndex = 5;
            btnBrowseWorkDir.Text = "📁";
            btnBrowseWorkDir.UseVisualStyleBackColor = false;
            btnBrowseWorkDir.Click += btnBrowseWorkDir_Click;
            // 
            // txtWorkingDirectory
            // 
            txtWorkingDirectory.Font = new Font("맑은 고딕", 9F);
            txtWorkingDirectory.Location = new Point(15, 169);
            txtWorkingDirectory.Name = "txtWorkingDirectory";
            txtWorkingDirectory.ReadOnly = true;
            txtWorkingDirectory.Size = new Size(195, 23);
            txtWorkingDirectory.TabIndex = 4;
            // 
            // lblWorkDir
            // 
            lblWorkDir.AutoSize = true;
            lblWorkDir.Font = new Font("맑은 고딕", 9F);
            lblWorkDir.Location = new Point(15, 151);
            lblWorkDir.Name = "lblWorkDir";
            lblWorkDir.Size = new Size(86, 15);
            lblWorkDir.TabIndex = 12;
            lblWorkDir.Text = "작업 디렉토리:";
            // 
            // cmbServiceType
            // 
            cmbServiceType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbServiceType.Font = new Font("맑은 고딕", 9F);
            cmbServiceType.FormattingEnabled = true;
            cmbServiceType.Items.AddRange(new object[] { "HTTP", "HTTPS", "TCP" });
            cmbServiceType.Location = new Point(90, 70);
            cmbServiceType.Name = "cmbServiceType";
            cmbServiceType.Size = new Size(170, 23);
            cmbServiceType.TabIndex = 3;
            cmbServiceType.SelectedIndexChanged += cmbServiceType_SelectedIndexChanged;
            // 
            // lblServiceType
            // 
            lblServiceType.AutoSize = true;
            lblServiceType.Font = new Font("맑은 고딕", 9F);
            lblServiceType.Location = new Point(15, 73);
            lblServiceType.Name = "lblServiceType";
            lblServiceType.Size = new Size(74, 15);
            lblServiceType.TabIndex = 2;
            lblServiceType.Text = "서비스 타입:";
            // 
            // chkLogFile
            // 
            chkLogFile.AutoSize = true;
            chkLogFile.Checked = true;
            chkLogFile.CheckState = CheckState.Checked;
            chkLogFile.Font = new Font("맑은 고딕", 9F);
            chkLogFile.Location = new Point(15, 245);
            chkLogFile.Name = "chkLogFile";
            chkLogFile.Size = new Size(106, 19);
            chkLogFile.TabIndex = 7;
            chkLogFile.Text = "로그 파일 저장";
            chkLogFile.UseVisualStyleBackColor = true;
            // 
            // chkHttps
            // 
            chkHttps.AutoSize = true;
            chkHttps.Font = new Font("맑은 고딕", 9F);
            chkHttps.Location = new Point(15, 220);
            chkHttps.Name = "chkHttps";
            chkHttps.Size = new Size(165, 19);
            chkHttps.TabIndex = 6;
            chkHttps.Text = "HTTPS (자체 서명 인증서)";
            chkHttps.UseVisualStyleBackColor = true;
            // 
            // cmbProtocol
            // 
            cmbProtocol.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProtocol.Font = new Font("맑은 고딕", 9F);
            cmbProtocol.FormattingEnabled = true;
            cmbProtocol.Items.AddRange(new object[] { "없음", "auto", "http2", "quic" });
            cmbProtocol.Location = new Point(90, 105);
            cmbProtocol.Name = "cmbProtocol";
            cmbProtocol.Size = new Size(170, 23);
            cmbProtocol.TabIndex = 5;
            // 
            // lblProtocol
            // 
            lblProtocol.AutoSize = true;
            lblProtocol.Font = new Font("맑은 고딕", 9F);
            lblProtocol.Location = new Point(15, 108);
            lblProtocol.Name = "lblProtocol";
            lblProtocol.Size = new Size(58, 15);
            lblProtocol.TabIndex = 4;
            lblProtocol.Text = "프로토콜:";
            // 
            // cmbLogLevel
            // 
            cmbLogLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLogLevel.Font = new Font("맑은 고딕", 9F);
            cmbLogLevel.FormattingEnabled = true;
            cmbLogLevel.Items.AddRange(new object[] { "없음", "debug", "info", "warn", "error", "fatal" });
            cmbLogLevel.Location = new Point(90, 70);
            cmbLogLevel.Name = "cmbLogLevel";
            cmbLogLevel.Size = new Size(170, 23);
            cmbLogLevel.TabIndex = 3;
            // 
            // lblLogLevel
            // 
            lblLogLevel.AutoSize = true;
            lblLogLevel.Font = new Font("맑은 고딕", 9F);
            lblLogLevel.Location = new Point(15, 73);
            lblLogLevel.Name = "lblLogLevel";
            lblLogLevel.Size = new Size(62, 15);
            lblLogLevel.TabIndex = 2;
            lblLogLevel.Text = "로그 레벨:";
            // 
            // txtPort
            // 
            txtPort.Font = new Font("맑은 고딕", 11F);
            txtPort.Location = new Point(90, 35);
            txtPort.Name = "txtPort";
            txtPort.Size = new Size(170, 27);
            txtPort.TabIndex = 1;
            txtPort.Text = "8000";
            txtPort.TextAlign = HorizontalAlignment.Center;
            // 
            // lblPort
            // 
            lblPort.AutoSize = true;
            lblPort.Font = new Font("맑은 고딕", 9F);
            lblPort.Location = new Point(15, 41);
            lblPort.Name = "lblPort";
            lblPort.Size = new Size(62, 15);
            lblPort.TabIndex = 0;
            lblPort.Text = "로컬 포트:";
            // 
            // grpStatus
            // 
            grpStatus.Controls.Add(btnCopyUrl);
            grpStatus.Controls.Add(lblUrl);
            grpStatus.Controls.Add(txtStatus);
            grpStatus.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            grpStatus.Location = new Point(12, 306);
            grpStatus.Name = "grpStatus";
            grpStatus.Size = new Size(276, 110);
            grpStatus.TabIndex = 5;
            grpStatus.TabStop = false;
            grpStatus.Text = "📡 상태";
            // 
            // btnCopyUrl
            // 
            btnCopyUrl.BackColor = Color.FromArgb(33, 150, 243);
            btnCopyUrl.Cursor = Cursors.Hand;
            btnCopyUrl.FlatAppearance.BorderSize = 0;
            btnCopyUrl.FlatStyle = FlatStyle.Flat;
            btnCopyUrl.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnCopyUrl.ForeColor = Color.White;
            btnCopyUrl.Location = new Point(85, 70);
            btnCopyUrl.Name = "btnCopyUrl";
            btnCopyUrl.Size = new Size(110, 30);
            btnCopyUrl.TabIndex = 5;
            btnCopyUrl.Text = "📋 URL 복사";
            btnCopyUrl.UseVisualStyleBackColor = false;
            btnCopyUrl.Click += btnCopyUrl_Click;
            // 
            // lblUrl
            // 
            lblUrl.AutoSize = true;
            lblUrl.Font = new Font("맑은 고딕", 9F);
            lblUrl.ForeColor = Color.FromArgb(100, 100, 100);
            lblUrl.Location = new Point(10, 23);
            lblUrl.Name = "lblUrl";
            lblUrl.Size = new Size(59, 15);
            lblUrl.TabIndex = 4;
            lblUrl.Text = "터널 URL:";
            // 
            // grpLog
            // 
            grpLog.Controls.Add(btnClearLog);
            grpLog.Controls.Add(txtLog);
            grpLog.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            grpLog.Location = new Point(299, 12);
            grpLog.Name = "grpLog";
            grpLog.Size = new Size(485, 577);
            grpLog.TabIndex = 6;
            grpLog.TabStop = false;
            grpLog.Text = "📝 로그";
            // 
            // btnClearLog
            // 
            btnClearLog.BackColor = Color.FromArgb(150, 150, 150);
            btnClearLog.Cursor = Cursors.Hand;
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.FlatStyle = FlatStyle.Flat;
            btnClearLog.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnClearLog.ForeColor = Color.White;
            btnClearLog.Location = new Point(6, 537);
            btnClearLog.Name = "btnClearLog";
            btnClearLog.Size = new Size(473, 30);
            btnClearLog.TabIndex = 3;
            btnClearLog.Text = "🗑️ 로그 초기화";
            btnClearLog.UseVisualStyleBackColor = false;
            btnClearLog.Click += btnClearLog_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(796, 601);
            Controls.Add(grpLog);
            Controls.Add(grpStatus);
            Controls.Add(grpSettings);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Name = "MainForm";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            Shown += MainForm_Shown;
            grpSettings.ResumeLayout(false);
            grpSettings.PerformLayout();
            grpStatus.ResumeLayout(false);
            grpStatus.PerformLayout();
            grpLog.ResumeLayout(false);
            grpLog.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button btnStart;
        private Button btnStop;
        private TextBox txtLog;
        private TextBox txtStatus;
        private GroupBox grpSettings;
        private Label lblPort;
        private TextBox txtPort;
        private Label lblLogLevel;
        private ComboBox cmbLogLevel;
        private Label lblProtocol;
        private ComboBox cmbProtocol;
        private CheckBox chkHttps;
        private CheckBox chkLogFile;
        private GroupBox grpStatus;
        private Label lblUrl;
        private Button btnCopyUrl;
        private GroupBox grpLog;
        private Button btnClearLog;
        private ComboBox cmbServiceType;
        private Label lblServiceType;
        private TextBox txtWorkingDirectory;
        private Label lblWorkDir;
        private Button btnBrowseWorkDir;
    }
}