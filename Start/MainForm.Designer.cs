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
            txtLog = new TextBox();
            grpLog = new GroupBox();
            panel3 = new Panel();
            btnClearLog = new Button();
            panel5 = new Panel();
            btnViewLogFile = new Button();
            panel6 = new Panel();
            btnSaveLog = new Button();
            panel1 = new Panel();
            panel2 = new Panel();
            btnStart = new Button();
            btnStop = new Button();
            grpStatus = new GroupBox();
            btnOpenBrowser = new Button();
            btnQRCode = new Button();
            lblUptime = new Label();
            label1 = new Label();
            btnCopyUrl = new Button();
            lblUrl = new Label();
            txtStatus = new TextBox();
            grpSettings = new GroupBox();
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
            panel4 = new Panel();
            grpLog.SuspendLayout();
            panel3.SuspendLayout();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            grpStatus.SuspendLayout();
            grpSettings.SuspendLayout();
            SuspendLayout();
            // 
            // txtLog
            // 
            txtLog.BackColor = Color.FromArgb(30, 30, 30);
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.ForeColor = Color.FromArgb(200, 200, 200);
            txtLog.Location = new Point(5, 21);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Size = new Size(506, 382);
            txtLog.TabIndex = 2;
            // 
            // grpLog
            // 
            grpLog.Controls.Add(txtLog);
            grpLog.Controls.Add(panel3);
            grpLog.Dock = DockStyle.Fill;
            grpLog.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            grpLog.Location = new Point(313, 10);
            grpLog.Name = "grpLog";
            grpLog.Padding = new Padding(5);
            grpLog.Size = new Size(516, 446);
            grpLog.TabIndex = 6;
            grpLog.TabStop = false;
            grpLog.Text = "📝 로그";
            // 
            // panel3
            // 
            panel3.Controls.Add(btnClearLog);
            panel3.Controls.Add(panel5);
            panel3.Controls.Add(btnViewLogFile);
            panel3.Controls.Add(panel6);
            panel3.Controls.Add(btnSaveLog);
            panel3.Dock = DockStyle.Bottom;
            panel3.Location = new Point(5, 403);
            panel3.Name = "panel3";
            panel3.Padding = new Padding(5);
            panel3.Size = new Size(506, 38);
            panel3.TabIndex = 11;
            // 
            // btnClearLog
            // 
            btnClearLog.BackColor = Color.FromArgb(150, 150, 150);
            btnClearLog.Cursor = Cursors.Hand;
            btnClearLog.Dock = DockStyle.Fill;
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.FlatStyle = FlatStyle.Flat;
            btnClearLog.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnClearLog.ForeColor = Color.White;
            btnClearLog.Location = new Point(5, 5);
            btnClearLog.Name = "btnClearLog";
            btnClearLog.Size = new Size(279, 28);
            btnClearLog.TabIndex = 4;
            btnClearLog.Text = "🗑️ 로그 초기화";
            btnClearLog.UseVisualStyleBackColor = false;
            btnClearLog.Click += btnClearLog_Click;
            // 
            // panel5
            // 
            panel5.Dock = DockStyle.Right;
            panel5.Location = new Point(284, 5);
            panel5.Name = "panel5";
            panel5.Size = new Size(5, 28);
            panel5.TabIndex = 12;
            // 
            // btnViewLogFile
            // 
            btnViewLogFile.BackColor = Color.Sienna;
            btnViewLogFile.Cursor = Cursors.Hand;
            btnViewLogFile.Dock = DockStyle.Right;
            btnViewLogFile.FlatAppearance.BorderSize = 0;
            btnViewLogFile.FlatStyle = FlatStyle.Flat;
            btnViewLogFile.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnViewLogFile.ForeColor = Color.White;
            btnViewLogFile.Location = new Point(289, 5);
            btnViewLogFile.Name = "btnViewLogFile";
            btnViewLogFile.Size = new Size(119, 28);
            btnViewLogFile.TabIndex = 6;
            btnViewLogFile.Text = "📂 로그 파일 열기";
            btnViewLogFile.UseVisualStyleBackColor = false;
            btnViewLogFile.Click += btnViewLogFile_Click;
            // 
            // panel6
            // 
            panel6.Dock = DockStyle.Right;
            panel6.Location = new Point(408, 5);
            panel6.Name = "panel6";
            panel6.Size = new Size(5, 28);
            panel6.TabIndex = 14;
            // 
            // btnSaveLog
            // 
            btnSaveLog.BackColor = Color.FromArgb(50, 150, 50);
            btnSaveLog.Cursor = Cursors.Hand;
            btnSaveLog.Dock = DockStyle.Right;
            btnSaveLog.FlatAppearance.BorderSize = 0;
            btnSaveLog.FlatStyle = FlatStyle.Flat;
            btnSaveLog.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnSaveLog.ForeColor = Color.White;
            btnSaveLog.Location = new Point(413, 5);
            btnSaveLog.Name = "btnSaveLog";
            btnSaveLog.Size = new Size(88, 28);
            btnSaveLog.TabIndex = 5;
            btnSaveLog.Text = "💾 로그 저장";
            btnSaveLog.UseVisualStyleBackColor = false;
            btnSaveLog.Click += btnSaveLog_Click;
            // 
            // panel1
            // 
            panel1.Controls.Add(panel2);
            panel1.Controls.Add(grpStatus);
            panel1.Controls.Add(grpSettings);
            panel1.Dock = DockStyle.Left;
            panel1.Location = new Point(10, 10);
            panel1.Name = "panel1";
            panel1.Size = new Size(293, 446);
            panel1.TabIndex = 7;
            // 
            // panel2
            // 
            panel2.Controls.Add(btnStart);
            panel2.Controls.Add(btnStop);
            panel2.Dock = DockStyle.Bottom;
            panel2.Location = new Point(0, 393);
            panel2.Name = "panel2";
            panel2.Padding = new Padding(10);
            panel2.Size = new Size(293, 53);
            panel2.TabIndex = 10;
            // 
            // btnStart
            // 
            btnStart.BackColor = Color.FromArgb(76, 175, 80);
            btnStart.Cursor = Cursors.Hand;
            btnStart.Dock = DockStyle.Left;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.Font = new Font("맑은 고딕", 10F, FontStyle.Bold);
            btnStart.ForeColor = Color.White;
            btnStart.Location = new Point(10, 10);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(135, 33);
            btnStart.TabIndex = 6;
            btnStart.Text = "🚀 터널 시작";
            btnStart.UseVisualStyleBackColor = false;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.BackColor = Color.FromArgb(244, 67, 54);
            btnStop.Cursor = Cursors.Hand;
            btnStop.Dock = DockStyle.Right;
            btnStop.Enabled = false;
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.FlatStyle = FlatStyle.Flat;
            btnStop.Font = new Font("맑은 고딕", 10F, FontStyle.Bold);
            btnStop.ForeColor = Color.White;
            btnStop.Location = new Point(148, 10);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(135, 33);
            btnStop.TabIndex = 7;
            btnStop.Text = "⏹ 터널 중지";
            btnStop.UseVisualStyleBackColor = false;
            btnStop.Click += btnStop_Click;
            // 
            // grpStatus
            // 
            grpStatus.Controls.Add(btnOpenBrowser);
            grpStatus.Controls.Add(btnQRCode);
            grpStatus.Controls.Add(lblUptime);
            grpStatus.Controls.Add(label1);
            grpStatus.Controls.Add(btnCopyUrl);
            grpStatus.Controls.Add(lblUrl);
            grpStatus.Controls.Add(txtStatus);
            grpStatus.Dock = DockStyle.Top;
            grpStatus.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            grpStatus.Location = new Point(0, 202);
            grpStatus.Name = "grpStatus";
            grpStatus.Size = new Size(293, 154);
            grpStatus.TabIndex = 9;
            grpStatus.TabStop = false;
            grpStatus.Text = "📡 상태";
            // 
            // btnOpenBrowser
            // 
            btnOpenBrowser.BackColor = Color.FromArgb(50, 150, 50);
            btnOpenBrowser.Cursor = Cursors.Hand;
            btnOpenBrowser.Enabled = false;
            btnOpenBrowser.FlatAppearance.BorderSize = 0;
            btnOpenBrowser.FlatStyle = FlatStyle.Flat;
            btnOpenBrowser.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnOpenBrowser.ForeColor = Color.White;
            btnOpenBrowser.Location = new Point(16, 111);
            btnOpenBrowser.Name = "btnOpenBrowser";
            btnOpenBrowser.Size = new Size(141, 30);
            btnOpenBrowser.TabIndex = 9;
            btnOpenBrowser.Text = "🌐 브라우저로 열기";
            btnOpenBrowser.UseVisualStyleBackColor = false;
            btnOpenBrowser.Click += btnOpenBrowser_Click;
            // 
            // btnQRCode
            // 
            btnQRCode.BackColor = Color.Gray;
            btnQRCode.Cursor = Cursors.Hand;
            btnQRCode.Enabled = false;
            btnQRCode.FlatAppearance.BorderSize = 0;
            btnQRCode.FlatStyle = FlatStyle.Flat;
            btnQRCode.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnQRCode.ForeColor = Color.White;
            btnQRCode.Location = new Point(226, 111);
            btnQRCode.Name = "btnQRCode";
            btnQRCode.Size = new Size(50, 30);
            btnQRCode.TabIndex = 8;
            btnQRCode.Text = "📱 QR";
            btnQRCode.UseVisualStyleBackColor = false;
            btnQRCode.Click += btnQRCode_Click;
            // 
            // lblUptime
            // 
            lblUptime.Font = new Font("맑은 고딕", 12F, FontStyle.Bold, GraphicsUnit.Point, 129);
            lblUptime.ForeColor = SystemColors.WindowFrame;
            lblUptime.Location = new Point(16, 34);
            lblUptime.Name = "lblUptime";
            lblUptime.Size = new Size(267, 30);
            lblUptime.TabIndex = 7;
            lblUptime.Text = "00:00:00";
            lblUptime.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("맑은 고딕", 9F);
            label1.ForeColor = Color.FromArgb(100, 100, 100);
            label1.Location = new Point(16, 19);
            label1.Name = "label1";
            label1.Size = new Size(62, 15);
            label1.TabIndex = 6;
            label1.Text = "실행 시간:";
            // 
            // btnCopyUrl
            // 
            btnCopyUrl.BackColor = Color.FromArgb(33, 150, 243);
            btnCopyUrl.Cursor = Cursors.Hand;
            btnCopyUrl.Enabled = false;
            btnCopyUrl.FlatAppearance.BorderSize = 0;
            btnCopyUrl.FlatStyle = FlatStyle.Flat;
            btnCopyUrl.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            btnCopyUrl.ForeColor = Color.White;
            btnCopyUrl.Location = new Point(163, 111);
            btnCopyUrl.Name = "btnCopyUrl";
            btnCopyUrl.Size = new Size(57, 30);
            btnCopyUrl.TabIndex = 5;
            btnCopyUrl.Text = "📋 복사";
            btnCopyUrl.UseVisualStyleBackColor = false;
            btnCopyUrl.Click += btnCopyUrl_Click;
            // 
            // lblUrl
            // 
            lblUrl.AutoSize = true;
            lblUrl.Font = new Font("맑은 고딕", 9F);
            lblUrl.ForeColor = Color.FromArgb(100, 100, 100);
            lblUrl.Location = new Point(16, 64);
            lblUrl.Name = "lblUrl";
            lblUrl.Size = new Size(59, 15);
            lblUrl.TabIndex = 4;
            lblUrl.Text = "터널 URL:";
            // 
            // txtStatus
            // 
            txtStatus.BackColor = Color.FromArgb(245, 245, 245);
            txtStatus.BorderStyle = BorderStyle.FixedSingle;
            txtStatus.Font = new Font("맑은 고딕", 9F, FontStyle.Bold, GraphicsUnit.Point, 129);
            txtStatus.ForeColor = Color.FromArgb(100, 100, 100);
            txtStatus.Location = new Point(16, 82);
            txtStatus.Name = "txtStatus";
            txtStatus.ReadOnly = true;
            txtStatus.Size = new Size(260, 23);
            txtStatus.TabIndex = 3;
            txtStatus.Text = "대기 중...";
            txtStatus.TextAlign = HorizontalAlignment.Center;
            // 
            // grpSettings
            // 
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
            grpSettings.Dock = DockStyle.Top;
            grpSettings.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            grpSettings.Location = new Point(0, 0);
            grpSettings.Name = "grpSettings";
            grpSettings.Size = new Size(293, 202);
            grpSettings.TabIndex = 8;
            grpSettings.TabStop = false;
            grpSettings.Text = "⚙️ 설정";
            // 
            // cmbServiceType
            // 
            cmbServiceType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbServiceType.Font = new Font("맑은 고딕", 9F);
            cmbServiceType.FormattingEnabled = true;
            cmbServiceType.Items.AddRange(new object[] { "HTTP", "HTTPS" });
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
            chkLogFile.Location = new Point(15, 170);
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
            chkHttps.Location = new Point(15, 145);
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
            // panel4
            // 
            panel4.Dock = DockStyle.Left;
            panel4.Location = new Point(303, 10);
            panel4.Name = "panel4";
            panel4.Size = new Size(10, 446);
            panel4.TabIndex = 8;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(839, 466);
            Controls.Add(grpLog);
            Controls.Add(panel4);
            Controls.Add(panel1);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(674, 470);
            Name = "MainForm";
            Padding = new Padding(10);
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            Shown += MainForm_Shown;
            grpLog.ResumeLayout(false);
            grpLog.PerformLayout();
            panel3.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            grpStatus.ResumeLayout(false);
            grpStatus.PerformLayout();
            grpSettings.ResumeLayout(false);
            grpSettings.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TextBox txtLog;
        private GroupBox grpLog;
        private Panel panel1;
        private GroupBox grpStatus;
        private Button btnCopyUrl;
        private Label lblUrl;
        private TextBox txtStatus;
        private GroupBox grpSettings;
        private ComboBox cmbServiceType;
        private Label lblServiceType;
        private CheckBox chkLogFile;
        private CheckBox chkHttps;
        private ComboBox cmbProtocol;
        private Label lblProtocol;
        private ComboBox cmbLogLevel;
        private Label lblLogLevel;
        private TextBox txtPort;
        private Label lblPort;
        private Button btnStop;
        private Button btnStart;
        private Panel panel2;
        private Panel panel3;
        private Button btnClearLog;
        private Label label1;
        private Label lblUptime;
        private Button btnSaveLog;
        private Button btnQRCode;
        private Panel panel4;
        private Button btnViewLogFile;
        private Panel panel5;
        private Panel panel6;
        private Button btnOpenBrowser;
    }
}