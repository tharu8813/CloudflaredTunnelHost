// Start/QRViewer.cs - 개선된 버전
namespace CloudflaredTunnelHost.Start {
    public partial class QRViewer : DevForm {
        private readonly string _url;
        private readonly string _encodedUrl;

        // [개선] 인코딩된 URL도 받아서 사용
        public QRViewer(string url, string encodedUrl) {
            InitializeComponent();
            _url = url;
            _encodedUrl = encodedUrl;
            this.Text = $"QR 코드 - {url}";
        }

        private void QRViewer_Load(object sender, EventArgs e) {
            try {
                // [버그수정] URL 인코딩 적용으로 특수문자 처리
                string qrApiUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={_encodedUrl}";
                pictureBox1.LoadAsync(qrApiUrl);

                // [개선] 로드 오류 이벤트 처리
                pictureBox1.LoadCompleted += (s, ev) => {
                    if (ev.Error != null) {
                        MessageBox.Show($"QR 코드 로드 실패: {ev.Error.Message}",
                            "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
            } catch (Exception ex) {
                MessageBox.Show($"QR 코드 생성 실패: {ex.Message}",
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // [버그수정] Deactivate(비활성화) 시 Dispose는 너무 공격적
        // 사용자가 잠깐 다른 창을 클릭해도 닫혀버리는 문제 수정 → FormClosed로 변경
        private void QRViewer_FormClosed(object sender, FormClosedEventArgs e) {
            Dispose();
        }
    }
}