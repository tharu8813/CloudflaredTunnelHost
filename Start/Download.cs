using CloudflaredTunnelHost.Tools;

namespace CloudflaredTunnelHost.Start {
    public partial class Download : DevForm {
        public Download() {
            InitializeComponent();
        }

        private async void Download_Load(object sender, EventArgs e) {
            bool success = await Tol.DownloadFileAsync(
                Tol.cloudflaredPath,
                @"https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe",
                progressBar1,
                label1);
            if (success) {
                DialogResult = DialogResult.OK;
            } else {
                DialogResult = DialogResult.Cancel;
            }
            Dispose();
        }
    }
}
