// Start/Download.cs - 개선된 버전
using CloudflaredTunnelHost.Tools;

namespace CloudflaredTunnelHost.Start {
    public partial class Download : DevForm {
        // [개선] 취소 토큰: 폼 닫힐 때 다운로드 중단 가능
        private CancellationTokenSource? _cts;

        public Download() {
            InitializeComponent();
            // [개선] Designer에 없는 이벤트 수동 연결
            this.FormClosing += Download_FormClosing;
            this.FormClosed += Download_FormClosed;
        }

        private async void Download_Load(object sender, EventArgs e) {
            _cts = new CancellationTokenSource();

            bool success = await Tol.DownloadFileAsync(
                Tol.cloudflaredPath,
                "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe",
                progressBar1,
                label1,
                _cts.Token); // [개선] 취소 토큰 전달

            // [버그수정] 폼이 이미 닫혔으면 DialogResult 설정 불필요
            if (!IsDisposed) {
                DialogResult = success ? DialogResult.OK : DialogResult.Cancel;
            }
        }

        // [버그수정] 폼 닫힐 때 진행 중인 다운로드 취소
        private void Download_FormClosing(object sender, FormClosingEventArgs e) {
            _cts?.Cancel();
        }

        // [개선] FormClosed에서 CancellationTokenSource 정리 (Designer의 Dispose와 충돌 없이)
        private void Download_FormClosed(object sender, FormClosedEventArgs e) {
            _cts?.Dispose();
            _cts = null;
        }
    }
}