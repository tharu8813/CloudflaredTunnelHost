namespace CloudflaredTunnelHost.Start {
    public partial class QRViewer : DevForm {
        private readonly string url;

        public QRViewer(string url) {
            InitializeComponent();
            this.url = url;
        }

        private void QRViewer_Load(object sender, EventArgs e) {
            pictureBox1.LoadAsync($@"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={url}");
        }

        private void QRViewer_Deactivate(object sender, EventArgs e) {
            Dispose();
        }
    }
}
