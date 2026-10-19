using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.Core.Services;
using CloudflaredTunnelHost.Services;
using CloudflaredTunnelHost.ViewModels;
using CloudflaredTunnelHost.Views;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var failures = new List<string>();
        var listener = new BindingErrors(); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        var root = Path.GetFullPath(Path.Combine(".test-data", "wpf-" + Guid.NewGuid().ToString("N")));
        var artifacts = Path.GetFullPath(".artifacts"); Directory.CreateDirectory(artifacts);
        app.Resources.MergedDictionaries.Add(Dictionary("Dark")); app.Resources.MergedDictionaries.Add(Dictionary("Controls"));
        MainViewModel? model = null;
        MainWindow? window = null;
        bool quittingViaModel = false;
        bool requireTray = !args.Contains("--allow-no-tray");
        app.Exit += (_, e) =>
        {
            if (quittingViaModel && window is not null)
            {
                var tray = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                if (window.IsVisible || (tray is not null && !(bool)tray.GetType().GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!)) failures.Add("Full exit did not close window and dispose tray.");
            }
            File.WriteAllText(Path.Combine(artifacts, "wpf-test-results.txt"), failures.Count == 0 ? "PASS" : string.Join("\n", failures));
            if (failures.Count > 0) e.ApplicationExitCode = 1;
        };
        app.DispatcherUnhandledException += (_, e) => { failures.Add(e.Exception.ToString()); e.Handled = true; app.Shutdown(1); };
        app.Startup += async (_, _) =>
        {
            try
            {
                model = new(new AppPaths(root)); window = new(model); window.Show();
                await Task.Delay(700);
                Assert(model.Logs.Count > 0, "startup log timer is alive");
                var trayAvailable = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) is not null;
                Assert(!model.Logs.Any(x => x.Level == "ERROR" && (requireTray || !x.Message.Contains("시스템 트레이"))), "startup service errors: " + string.Join("\n", model.Logs.Where(x => x.Level == "ERROR")));
                Assert(trayAvailable || !requireTray, "native tray was created");
                if (!trayAvailable) Console.WriteLine("SKIP native tray integration: no accessible Windows shell (CI mode)");
                Assert(model.StartCommand.CanExecute(null) && !model.CopyUrlCommand.CanExecute(null), "Idle command enablement");
                window.Width = 860; window.Height = 640;
                typeof(MainViewModel).GetProperty(nameof(model.IsWorking))!.SetValue(model, true);
                typeof(MainViewModel).GetMethod("SetWork", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, ["다운로드 진행률 표시 검증"]);
                await Task.Delay(250); window.UpdateLayout();
                var workPanel = (FrameworkElement)window.FindName("WorkPanel");
                var contentRoot = (FrameworkElement)window.Content;
                var position = workPanel.TransformToAncestor(contentRoot).Transform(new Point());
                Assert(workPanel.IsVisible && workPanel.ActualHeight > 0 && position.Y >= 0 && position.Y + workPanel.ActualHeight <= contentRoot.ActualHeight, "download progress remains visible at minimum window size");
                Render(window, Path.Combine(artifacts, "download-Dark-860.png"), 96);
                typeof(MainViewModel).GetProperty(nameof(model.IsWorking))!.SetValue(model, false);
                foreach (var file in Directory.GetFiles("Assets/Icons", "*.svg"))
                    Assert(!CloudflaredTunnelHost.Controls.SvgIcon.LoadGeometry(Path.GetFileNameWithoutExtension(file)).IsEmpty(), "SVG parsed");
                using var png = new MemoryStream();
                var qr = DesktopService.MakeQr("https://test.trycloudflare.com");
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(qr)); encoder.Save(png);
                Assert(qr.PixelWidth >= 600 && png.Length > 100, "local high-resolution QR generation");
                File.WriteAllBytes(Path.Combine(artifacts, "qr.png"), png.ToArray());
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    app.Resources.MergedDictionaries[0] = Dictionary(theme); model.Theme = theme;
                    foreach (var size in new[] { (1180, 860), (860, 640) })
                    {
                        window.Width = size.Item1; window.Height = size.Item2; model.IsAdvanced = false;
                        await Task.Delay(250); window.UpdateLayout();
                        Render(window, Path.Combine(artifacts, $"dashboard-{theme}-{size.Item1}.png"), 96);
                        foreach (var dpi in new[] { 120d, 144d, 192d }) Render(window, Path.Combine(artifacts, $"dashboard-{theme}-{size.Item1}-{dpi}.png"), dpi);
                    }
                    model.IsAdvanced = true; window.Width = 1180; window.Height = 860;
                    await Task.Delay(250); Render(window, Path.Combine(artifacts, $"settings-{theme}.png"), 96);
                }
                model.IsAdvanced = false;
                // Inject a service snapshot only in this test to verify bindings without publishing a fake tunnel.
                typeof(MainViewModel).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model,
                    new TunnelSnapshot(TunnelState.Connecting, "로컬 서버 대기 테스트", "https://test.trycloudflare.com", EdgeReady: true, LocalReady: false));
                typeof(MainViewModel).GetMethod("RefreshState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, null);
                Assert(model.StateLabel == "로컬 서버 대기" && model.HasUrl && model.CanStop && !model.IsConnecting, "offline origin preserves a usable tunnel URL and shows a waiting state");
                await Task.Delay(250); Render(window, Path.Combine(artifacts, "waiting-for-server-Light.png"), 96);
                typeof(MainViewModel).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model,
                    new TunnelSnapshot(TunnelState.Connected, "연결 테스트", "https://a-very-long-readable-development-tunnel-address.trycloudflare.com", DateTimeOffset.UtcNow, Reachability: "HTTP 200 확인"));
                typeof(MainViewModel).GetMethod("RefreshState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, null);
                Assert(model.CopyUrlCommand.CanExecute(null), "URL command enablement");
                await Task.Delay(250); Render(window, Path.Combine(artifacts, "connected-Light.png"), 96);
                model.ShowQrCommand.Execute(null); await Task.Delay(250);
                Assert(app.Windows.OfType<QrWindow>().Any(), "QR window opens");
                app.Windows.OfType<QrWindow>().First().Close();
                for (int i = 0; i < 10000; i++) model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "stress log " + i)]);
                Assert(model.Logs.Count == 2000, "UI log collection bounded at 2000");
                model.Search = "9999"; Assert(model.FilteredLogs.Cast<LogEntry>().Count() == 1, "log search");
                model.Search = ""; model.LevelFilter = "ERROR"; Assert(model.FilteredLogs.IsEmpty, "log level filter");
                model.LevelFilter = "전체"; model.LogsOpen = true;
                app.Resources.MergedDictionaries[0] = Dictionary("Dark"); model.Theme = "Dark";
                ((ScrollViewer)window.FindName("ContentScroll")).ScrollToEnd();
                await Task.Delay(300);
                var list = (ListBox)window.FindName("ActivityLog");
                var realized = Enumerable.Range(0, model.Logs.Count).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) is not null);
                Assert(realized < 100, "log list virtualization");
                Render(window, Path.Combine(artifacts, "logs-Dark.png"), 96);
                Assert(listener.Errors.Count == 0, "no WPF binding errors: " + string.Join("\n", listener.Errors));
                quittingViaModel = true;
                var tray = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                if (tray is not null)
                {
                    var menu = (ContextMenu)tray.GetType().GetField("_menu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
                    menu.Items.OfType<MenuItem>().First(item => Equals(item.Header, "프로그램 완전 종료")).Command.Execute(null);
                }
                else model.ExitCommand.Execute(null);
                Console.WriteLine("PASS WPF startup, SVG assets, QR generation/window, command states, log stress/search/filter, dark/light layouts, 125/150/200% raster rendering, shutdown");
            }
            catch (Exception ex) { failures.Add(ex.ToString()); Console.WriteLine("FAIL " + ex); }
            finally
            {
                if (!quittingViaModel)
                {
                    if (model is not null && window?.IsVisible == true) { await model.ShutdownAsync(); window.Hide(); }
                    app.Shutdown(failures.Count == 0 ? 0 : 1);
                }
            }
        };
        return app.Run();
    }
    private static ResourceDictionary Dictionary(string name) => new() { Source = new Uri($"pack://application:,,,/CloudflaredTunnelHost;component/Themes/{name}.xaml") };
    private static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Render(Window window, string path, double dpi)
    {
        var root = (FrameworkElement)window.Content;
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * dpi / 96), (int)Math.Ceiling(root.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if (message?.Contains("Error:") == true) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
