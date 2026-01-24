using CloudflaredTunnelHost.Tools;

namespace CloudflaredTunnelHost.Start
{
    public partial class MainForm : DevForm
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (!File.Exists(Path.Combine(Tol.path, "cloudflared.exe")))
            {

                if (Tol.ShowQuestion("cloudflared.exe가 존재하지 않습니다. 다운받으시겠습니까?") == DialogResult.Yes)
                {
                    if (new Download().ShowDialog() != DialogResult.OK)
                    {
                        Application.Exit();
                    }
                }
                else
                {
                    Application.Exit();
                }
            }
        }
    }
