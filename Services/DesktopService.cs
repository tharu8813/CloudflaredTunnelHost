using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;

namespace CloudflaredTunnelHost.Services;

public static class DesktopService
{
    public static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    public static void Copy(string text) => Clipboard.SetText(text);
    public static BitmapSource MakeQr(string url)
    {
        using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        using var stream = new MemoryStream(qr.GetGraphic(20));
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
    public static void SaveQr(BitmapSource source)
    {
        var dialog = new SaveFileDialog { Filter = "PNG 이미지|*.png", FileName = "tunnel-qr.png" };
        if (dialog.ShowDialog() != true) return;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var file = File.Create(dialog.FileName); encoder.Save(file);
    }
}
