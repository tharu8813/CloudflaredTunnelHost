using CloudflaredTunnelHost.Tools;

namespace CloudflaredTunnelHost.Start
{
    public partial class Download : DevForm
    {
        public Download()
        {
            InitializeComponent();
        }

        private void Download_Load(object sender, EventArgs e)
        {
            bool success = Tol.DownloadFileAsync(
                Tol.path + @"cloudflared.exe",
                @"https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe",
                progressBar1,
                label1);

        }
    }
}
