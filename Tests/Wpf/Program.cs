using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
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
        bool windowClosed = false;
        bool closeWindow = args.Contains("--close-window");
        bool requireTray = !args.Contains("--allow-no-tray");
        app.Exit += (_, e) =>
        {
            if (quittingViaModel && window is not null)
            {
                var tray = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                if (!windowClosed || window.IsVisible || app.Windows.OfType<LogWindow>().Any() || (tray is not null && !(bool)tray.GetType().GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!)) failures.Add("Full exit did not close main/log windows and dispose tray.");
            }
            File.WriteAllText(Path.Combine(artifacts, closeWindow ? "wpf-close-results.txt" : "wpf-test-results.txt"), failures.Count == 0 ? "PASS" : string.Join("\n", failures));
            if (failures.Count > 0) e.ApplicationExitCode = 1;
        };
        app.DispatcherUnhandledException += (_, e) => { failures.Add(e.Exception.ToString()); e.Handled = true; app.Shutdown(1); };
        app.Startup += async (_, _) =>
        {
            try
            {
                model = new(new AppPaths(root)); window = new(model); window.Show();
                window.Closed += (_, _) => windowClosed = true;
                await Task.Delay(700);
                Assert(model.Logs.Count > 0, "startup log timer is alive");
                var trayAvailable = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) is not null;
                Assert(!model.Logs.Any(x => x.Level == "ERROR" && (requireTray || !x.Message.Contains("시스템 트레이"))), "startup service errors: " + string.Join("\n", model.Logs.Where(x => x.Level == "ERROR")));
                Assert(trayAvailable || !requireTray, "native tray was created");
                if (!trayAvailable) Console.WriteLine("SKIP native tray integration: no accessible Windows shell (CI mode)");
                Assert(model.StartCommand.CanExecute(null) && !model.CopyUrlCommand.CanExecute(null), "Idle command enablement");
                if (closeWindow)
                {
                    model.ShowLogsCommand.Execute(null);
                    Assert(app.Windows.OfType<LogWindow>().Count() == 1, "log popup is open before title-bar exit");
                    quittingViaModel = true;
                    window.Close(); window.Close();
                    Assert(!windowClosed, "Closing defers cleanup until the closing event has returned");
                    Console.WriteLine("PASS queued title-bar close with an idle tunnel, repeated close requests, and an open log window");
                    return;
                }
                if (trayAvailable)
                {
                    model.CloseToTray = true; window.Close(); await Task.Delay(100);
                    Assert(!window.IsVisible && !windowClosed, "title-bar close hides to tray without quitting");
                    typeof(MainWindow).GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                    Assert(window.IsVisible, "tray restore remains usable after closing is cancelled");
                    model.CloseToTray = false;
                }
                var portInput = (TextBox)window.FindName("PortInput");
                foreach (var port in new[] { "", "0", "65536", "abc", "-1" })
                {
                    model.PortText = port;
                    Assert(model.PortError.Length > 0 && !model.StartCommand.CanExecute(null) && !model.SaveSettingsCommand.CanExecute(null), "invalid port blocks start/save: " + port);
                    Assert(Validation.GetHasError(portInput), "port validation reaches the input border");
                }
                foreach (var port in new[] { "1", "65535", "3000" })
                {
                    model.PortText = port;
                    Assert(model.StartCommand.CanExecute(null) && !Validation.GetHasError(portInput), "valid port restores commands and input: " + port);
                }
                model.PortText = "8000";
                foreach (var retry in new[] { "", "-1", "11", "xx" })
                {
                    model.RetryText = retry;
                    Assert(model.RetryError.Length > 0 && !model.StartCommand.CanExecute(null), "invalid retry blocks start: " + retry);
                }
                model.RetryText = "0"; Assert(!model.HasInputErrors, "zero retries is valid");
                model.RetryText = "10"; Assert(!model.HasInputErrors, "ten retries is valid");
                model.RetryText = "3";
                model.AllowedEmails = "invalid-address";
                Assert(model.EmailError.Length > 0 && !model.StartCommand.CanExecute(null) && !model.SaveSettingsCommand.CanExecute(null), "invalid email blocks start/save");
                model.AllowedEmails = "dev@example.com"; Assert(!model.HasInputErrors, "valid email restores commands");
                model.AllowedEmails = ""; Assert(!model.HasInputErrors, "public access remains valid");
                window.Width = 860; window.Height = 640;
                await Task.Delay(250); window.UpdateLayout();
                var contentScroll = (ScrollViewer)window.FindName("ContentScroll");
                AssertVisibleWithin(portInput, contentScroll, "local port is visible without scrolling at minimum size");
                Assert(model.LogsOpen, "main activity log is visible by default");
                AssertVisibleWithin((FrameworkElement)window.FindName("LogPanel"), (FrameworkElement)window.Content, "main log panel is visible without scrolling at minimum size");
                Assert(Math.Abs(portInput.ActualHeight - ((ComboBox)window.FindName("ServiceTypeInput")).ActualHeight) < 2, "service and port inputs share the same height");
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
                    model.IsAdvanced = true;
                    foreach (var size in new[] { (1180, 860), (860, 640) })
                    {
                        window.Width = size.Item1; window.Height = size.Item2;
                        contentScroll.ScrollToTop();
                        await Task.Delay(250);
                        AssertVisibleWithin((Button)window.FindName("SaveSettingsButton"), (FrameworkElement)window.Content, "settings save remains visible");
                        Render(window, Path.Combine(artifacts, $"settings-{theme}-{size.Item1}.png"), 96);
                        contentScroll.ScrollToEnd(); await Task.Delay(100);
                        Render(window, Path.Combine(artifacts, $"settings-bottom-{theme}-{size.Item1}.png"), 96);
                    }
                    Assert(contentScroll.VerticalOffset > 0, "settings is scrolled before navigation");
                    model.ShowDashboardCommand.Execute(null); await Task.Delay(100);
                    Assert(contentScroll.VerticalOffset == 0, "dashboard navigation resets scroll");
                    model.LogsOpen = true; contentScroll.ScrollToEnd(); await Task.Delay(100);
                    model.ShowSettingsCommand.Execute(null); await Task.Delay(100);
                    Assert(contentScroll.VerticalOffset == 0, "settings navigation resets scroll");
                    model.LogsOpen = true;
                }
                model.IsAdvanced = false;
                model.PortText = "70000"; await Task.Delay(250);
                Render(window, Path.Combine(artifacts, "invalid-port-Light-860.png"), 96);
                model.IsAdvanced = true; model.RetryText = "11"; model.AllowedEmails = "invalid-address";
                await Task.Delay(250);
                var emailsInput = (TextBox)window.FindName("AllowedEmailsInput");
                var retryInput = (TextBox)window.FindName("RetryInput");
                Assert(Validation.GetHasError(emailsInput) && Validation.GetHasError(retryInput), "email and retry validation reaches both fields");
                emailsInput.BringIntoView(new Rect(0, 0, emailsInput.ActualWidth, emailsInput.ActualHeight + 60)); await Task.Delay(100);
                Render(window, Path.Combine(artifacts, "invalid-email-Light-860.png"), 96);
                retryInput.BringIntoView(new Rect(0, 0, retryInput.ActualWidth, retryInput.ActualHeight + 60)); await Task.Delay(100);
                Render(window, Path.Combine(artifacts, "invalid-retry-Light-860.png"), 96);
                model.PortText = "8000"; model.RetryText = "3"; model.AllowedEmails = ""; model.IsAdvanced = false;
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
                var qrWindow = app.Windows.OfType<QrWindow>().First();
                Render(qrWindow, Path.Combine(artifacts, "qr-window-Light.png"), 96);
                qrWindow.Width = 360; qrWindow.Height = 480; await Task.Delay(100);
                AssertVisibleWithin((Button)qrWindow.FindName("CopyImageButton"), (FrameworkElement)qrWindow.Content, "QR copy action remains visible at minimum size");
                AssertVisibleWithin((Button)qrWindow.FindName("CloseButton"), (FrameworkElement)qrWindow.Content, "QR close action remains visible at minimum size");
                Render(qrWindow, Path.Combine(artifacts, "qr-window-Light-360.png"), 96);
                qrWindow.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(qrWindow), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert(!app.Windows.OfType<QrWindow>().Any(), "Escape closes the QR window");
                for (int i = 0; i < 10000; i++) model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "stress log " + i)]);
                Assert(model.Logs.Count == 2000, "UI log collection bounded at 2000");
                model.ShowLogsCommand.Execute(null); await Task.Delay(100);
                var logWindow = app.Windows.OfType<LogWindow>().Single();
                var popupList = (ListBox)logWindow.FindName("ActivityLog");
                Assert(ReferenceEquals(logWindow.DataContext, model) && popupList.Items.Count == 2000, "popup shares the live log model");
                logWindow.WindowState = WindowState.Minimized;
                model.ShowLogsCommand.Execute(null);
                Assert(app.Windows.OfType<LogWindow>().Count() == 1 && logWindow.WindowState == WindowState.Normal, "reopening restores the existing popup");
                model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "live popup update")]);
                Assert(((LogEntry)popupList.Items[popupList.Items.Count - 1]).Message == "live popup update", "new logs reach the popup immediately");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    app.Resources.MergedDictionaries[0] = Dictionary(theme); model.Theme = theme;
                    foreach (var size in new[] { (940, 680), (620, 440) })
                    {
                        logWindow.Width = size.Item1; logWindow.Height = size.Item2; await Task.Delay(150);
                        Render(logWindow, Path.Combine(artifacts, $"log-window-{theme}-{size.Item1}.png"), 96);
                    }
                }
                model.Search = "9999"; Assert(model.FilteredLogs.Cast<LogEntry>().Count() == 1 && model.CopyLogsCommand.CanExecute(null) && model.SaveLogsCommand.CanExecute(null), "log search and export enablement");
                model.Search = ""; model.LevelFilter = "ERROR"; Assert(model.FilteredLogs.IsEmpty && !model.HasFilteredLogs && !model.CopyLogsCommand.CanExecute(null) && !model.SaveLogsCommand.CanExecute(null), "empty filter disables exports");
                Assert(popupList.Items.Count == 0 && ((ListBox)window.FindName("ActivityLog")).Items.Count == 2000, "popup filters do not hide the main recent log preview");
                model.LogsOpen = true; contentScroll.ScrollToEnd(); await Task.Delay(250);
                Render(logWindow, Path.Combine(artifacts, "logs-empty-Dark.png"), 96);
                model.LevelFilter = "전체"; model.LogsOpen = true;
                app.Resources.MergedDictionaries[0] = Dictionary("Dark"); model.Theme = "Dark";
                ((ScrollViewer)window.FindName("ContentScroll")).ScrollToEnd();
                await Task.Delay(300);
                var list = (ListBox)window.FindName("ActivityLog");
                var realized = Enumerable.Range(0, model.Logs.Count).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) is not null);
                Assert(realized < 100, "log list virtualization");
                var popupRealized = Enumerable.Range(0, model.Logs.Count).Count(index => popupList.ItemContainerGenerator.ContainerFromIndex(index) is not null);
                Assert(popupRealized < 100, "popup log list virtualization");
                Render(window, Path.Combine(artifacts, "logs-Dark.png"), 96);
                logWindow.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(logWindow), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert(!app.Windows.OfType<LogWindow>().Any() && window.IsVisible, "Escape closes only the log popup");
                model.ShowLogsCommand.Execute(null);
                Assert(app.Windows.OfType<LogWindow>().Count() == 1, "log popup can reopen after close");
                model.ClearLogsCommand.Execute(null);
                Assert(!model.HasFilteredLogs && !model.ClearLogsCommand.CanExecute(null), "clearing logs updates the empty state and command");
                Assert(listener.Errors.Count == 0, "no WPF binding errors: " + string.Join("\n", listener.Errors));
                quittingViaModel = true;
                var tray = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                if (tray is not null)
                {
                    var menu = (ContextMenu)tray.GetType().GetField("_menu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
                    menu.Items.OfType<MenuItem>().First(item => Equals(item.Header, "프로그램 완전 종료")).Command.Execute(null);
                }
                else model.ExitCommand.Execute(null);
                Console.WriteLine("PASS WPF startup, inline validation, navigation scroll, fixed actions, SVG assets, QR/Escape, command states, log stress/search/filter/empty state, dark/light layouts, 125/150/200% raster rendering, shutdown");
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
    private static void AssertVisibleWithin(FrameworkElement element, FrameworkElement viewport, string message)
    {
        element.UpdateLayout(); viewport.UpdateLayout();
        var bounds = element.TransformToAncestor(viewport).TransformBounds(new Rect(element.RenderSize));
        Assert(element.IsVisible && bounds.Top >= 0 && bounds.Bottom <= viewport.ActualHeight && bounds.Left >= 0 && bounds.Right <= viewport.ActualWidth, $"{message} ({bounds} in {viewport.RenderSize})");
    }
    private static void Render(Window window, string path, double dpi)
    {
        var root = (FrameworkElement)window.Content;
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * dpi / 96), (int)Math.Ceiling(root.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        var offset = VisualTreeHelper.GetOffset(root);
        using (var drawing = background.RenderOpen())
        {
            drawing.DrawRectangle(window.Background, null, new Rect(root.RenderSize));
            drawing.DrawRectangle(new VisualBrush(root) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(offset.X, offset.Y, root.ActualWidth, root.ActualHeight) }, null, new Rect(root.RenderSize));
        }
        bitmap.Render(background);
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
