using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Shell;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.Core.Services;
using CloudflaredTunnelHost.Infrastructure;
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
                if (!windowClosed || window.IsVisible || app.Windows.Cast<Window>().Any(other => other != window) || (tray is not null && !(bool)tray.GetType().GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!)) failures.Add("Full exit did not close the application windows and dispose tray.");
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
                AssertTunnelNotifications();
                var startButton = (Button)window.FindName("StartButton");
                var stopButton = (Button)window.FindName("StopButton");
                AssertCaptionState(window, model);
                Assert(startButton.IsVisible && !stopButton.IsVisible, "idle shows only the start action");
                Assert(((FrameworkElement)window.FindName("SharePlaceholder")).IsVisible && !((FrameworkElement)window.FindName("ShareActions")).IsVisible, "idle explains where the public URL will appear");
                if (closeWindow)
                {
                    model.ShowLogsCommand.Execute(null);
                    Assert(model.IsLogs && app.Windows.Count == 1, "logs are integrated into the main window before exit");
                    quittingViaModel = true;
                    RespondToExit(prompt => ((Button)prompt.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                    var closeCaption = (Button)window.FindName("CloseCaptionButton");
                    closeCaption.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    closeCaption.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(!windowClosed, "Closing defers cleanup until the closing event has returned");
                    Console.WriteLine("PASS queued title-bar close with an idle tunnel, repeated close requests, and integrated logs");
                    return;
                }
                var maximizeCaption = (Button)window.FindName("MaximizeCaptionButton");
                maximizeCaption.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(150);
                Assert(window.WindowState == WindowState.Maximized && Equals(maximizeCaption.ToolTip, "이전 크기로"), "custom maximize button updates its restore action");
                AssertVisibleWithin((Button)window.FindName("CloseCaptionButton"), (FrameworkElement)window.Content, "caption close stays within the maximized window");
                maximizeCaption.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(150);
                Assert(window.WindowState == WindowState.Normal && Equals(maximizeCaption.ToolTip, "최대화"), "custom restore button returns to normal size");
                var minimizeToTray = model.MinimizeToTray; model.MinimizeToTray = false;
                ((Button)window.FindName("MinimizeCaptionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100);
                Assert(window.WindowState == WindowState.Minimized, "custom minimize button uses the Windows minimize command");
                SystemCommands.RestoreWindow(window); await Task.Delay(100);
                model.MinimizeToTray = minimizeToTray;
                if (trayAvailable)
                {
                    model.CloseToTray = true;
                    ((Button)window.FindName("CloseCaptionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100);
                    Assert(!window.IsVisible && !windowClosed, "title-bar close hides to tray without quitting");
                    typeof(MainWindow).GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                    Assert(window.IsVisible, "tray restore remains usable after closing is cancelled");
                    model.CloseToTray = false;
                }
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    app.Resources.MergedDictionaries[0] = Dictionary(theme); model.Theme = theme;
                    RespondToExit(prompt =>
                    {
                        Assert(ComponentDispatcher.IsThreadModal && ((Button)prompt.FindName("CancelButton")).IsDefault, "exit confirmation is modal and defaults to continued use");
                        model.ExitCommand.Execute(null);
                        Assert(app.Windows.OfType<ExitConfirmationWindow>().Count() == 1, "repeated exit requests reuse the open confirmation");
                        Render(prompt, Path.Combine(artifacts, $"exit-confirmation-{theme}.png"), 96);
                        ((Button)prompt.FindName(theme == "Dark" ? "CancelButton" : "CloseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    });
                    model.ExitCommand.Execute(null);
                    await Task.Delay(50);
                    Assert(window.IsVisible && window.IsEnabled && !windowClosed, $"cancelling exit keeps the application running (visible {window.IsVisible}, enabled {window.IsEnabled}, closed {windowClosed})");
                }
                RespondToExit(prompt => ((Button)prompt.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                ((Button)window.FindName("CloseCaptionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(100);
                Assert(window.IsVisible && !windowClosed, "cancelling a title-bar close preserves the main window");
                var shownMain = false;
                var trayMenu = new TrayMenu(model, () => shownMain = true) { PlacementTarget = window, Placement = PlacementMode.Center };
                foreach (var theme in new[] { "Dark", "Light" })
                {
                    app.Resources.MergedDictionaries[0] = Dictionary(theme); model.Theme = theme;
                    trayMenu.IsOpen = true; await Task.Delay(150);
                    Assert(((MenuItem)trayMenu.FindName("StartItem")).IsVisible && !((MenuItem)trayMenu.FindName("StopItem")).IsVisible && !((MenuItem)trayMenu.FindName("CopyUrlItem")).IsEnabled, "idle tray menu reflects available actions");
                    Assert(((SolidColorBrush)trayMenu.Background).Color == ((SolidColorBrush)app.FindResource("SurfaceBrush")).Color, "tray menu follows the application theme");
                    var mainItem = (MenuItem)trayMenu.FindName("ShowMainItem");
                    mainItem.Focus(); mainItem.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
                    Assert(((MenuItem)trayMenu.FindName("StartItem")).IsKeyboardFocused, "tray menu supports keyboard navigation between actions");
                    RenderElement(trayMenu, trayMenu.Background, Path.Combine(artifacts, $"tray-menu-idle-{theme}.png"), 96);
                    ((MenuItem)trayMenu.FindName("ShowMainItem")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert(shownMain, "tray menu restores the main window");
                    trayMenu.IsOpen = false;
                }
                var portInput = (TextBox)window.FindName("PortInput");
                foreach (var port in new[] { "", "0", "65536", "abc", "-1" })
                {
                    model.PortText = port;
                    Assert(model.PortError.Length > 0 && !model.StartCommand.CanExecute(null) && !model.SaveSettingsCommand.CanExecute(null), "invalid port blocks start/save: " + port);
                    Assert(Validation.GetHasError(portInput), "port validation reaches the input border");
                }
                RespondToExit(prompt =>
                {
                    Assert(((TextBlock)prompt.FindName("UnsavedWarning")).IsVisible, "exit confirmation warns when invalid settings cannot be saved");
                    ((Button)prompt.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                model.ExitCommand.Execute(null);
                Assert(model.PortText == "-1" && window.IsVisible, "cancelling exit preserves the input draft");
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
                var reviewInput = (Button)window.FindName("ReviewInputButton");
                model.PortText = "invalid"; model.IsAdvanced = true;
                reviewInput.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(model.IsDashboard && portInput.IsKeyboardFocused && portInput.SelectedText == "invalid" && model.InputError == model.PortError, "error action opens and selects the invalid port");
                model.PortText = "8000"; model.AllowedEmails = "invalid-address";
                reviewInput.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(model.IsAdvanced && ((TextBox)window.FindName("AllowedEmailsInput")).IsKeyboardFocused && model.InputError == model.EmailError, "error action opens advanced settings at the invalid email");
                Assert(((TabItem)window.FindName("AccessTab")).IsSelected, "email errors open the access category");
                model.AllowedEmails = ""; model.RetryText = "11"; model.IsAdvanced = false;
                reviewInput.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(model.IsAdvanced && ((TextBox)window.FindName("RetryInput")).IsKeyboardFocused && model.InputError == model.RetryError, "error action scrolls to the invalid retry count");
                Assert(((TabItem)window.FindName("ConnectionTab")).IsSelected, "retry errors open the connection category");
                model.RetryText = "3"; model.IsAdvanced = false;
                Assert(model.InputError == "" && !reviewInput.IsVisible, "correcting inputs removes the error action");
                startButton.Focus();
                ((ScrollViewer)window.FindName("ContentScroll")).ScrollToTop();
                window.Width = 860; window.Height = 640;
                await Task.Delay(250); window.UpdateLayout();
                var contentScroll = (ScrollViewer)window.FindName("ContentScroll");
                AssertVisibleWithin(portInput, contentScroll, "local port is visible without scrolling at minimum size");
                Assert(model.LogsOpen, "main activity log is visible by default");
                AssertVisibleWithin((FrameworkElement)window.FindName("LogPanel"), (FrameworkElement)window.Content, "main log panel is visible without scrolling at minimum size");
                Assert(Math.Abs(portInput.ActualHeight - ((ComboBox)window.FindName("ServiceTypeInput")).ActualHeight) < 2, "service and port inputs share the same height");
                portInput.Focus(); portInput.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Assert(startButton.IsKeyboardFocused, "Tab moves from the port directly to start, before the lower log panel");
                typeof(MainViewModel).GetProperty(nameof(model.IsWorking))!.SetValue(model, true);
                model.IsAdvanced = true;
                AssertRuntimeSettings(window, model);
                var saveLog = (CheckBox)window.FindName("SaveLogFileInput"); saveLog.IsChecked = true;
                Assert(model.SaveLogFile && ((LoggingService)typeof(MainViewModel).GetField("_logs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!).SaveToFile, "log-file preference takes effect during startup work");
                saveLog.IsChecked = false;
                model.IsAdvanced = false;
                Assert(!startButton.IsVisible && stopButton.IsVisible && model.StopLabel == "작업 취소", "installation replaces start with an actionable cancel button");
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
                model.IsAdvanced = true; await Task.Delay(100);
                var settingsTabs = (TabControl)window.FindName("SettingsTabs");
                var settingsScroll = (ScrollViewer)window.FindName("ConnectionSettingsScroll");
                await CheckSmoothScroll(settingsScroll, "connection settings");
                Wheel(settingsScroll, -120);
                model.ShowDashboardCommand.Execute(null); await Task.Delay(250);
                Assert(settingsScroll.VerticalOffset == 0 && !settingsScroll.HasAnimatedProperties, "navigation cancels settings scroll animation");
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
                        await Task.Delay(250);
                        AssertVisibleWithin((Button)window.FindName("SaveSettingsButton"), (FrameworkElement)window.Content, "settings save remains visible");
                        Assert(!startButton.IsVisible, "settings focuses on saving rather than starting a tunnel");
                        Assert(settingsTabs.Items.Count == 5, "advanced settings has five categories");
                        foreach (TabItem tab in settingsTabs.Items)
                        {
                            settingsTabs.SelectedItem = tab; window.UpdateLayout(); await Task.Delay(100);
                            AssertVisibleWithin(tab, (FrameworkElement)window.Content, "every category header stays visible");
                            Assert(((ScrollViewer)tab.Content).IsVisible, "selected category content is visible");
                            Render(window, Path.Combine(artifacts, $"settings-{tab.Name}-{theme}-{size.Item1}.png"), 96);
                            ((ScrollViewer)tab.Content).ScrollToEnd(); await Task.Delay(50);
                            AssertVisibleWithin(tab, (FrameworkElement)window.Content, "category header remains visible while scrolling");
                            AssertVisibleWithin((Button)window.FindName("SaveSettingsButton"), (FrameworkElement)window.Content, "save remains visible in every category");
                        }
                    }
                    settingsTabs.SelectedItem = window.FindName("ConnectionTab"); window.UpdateLayout();
                    settingsScroll.ScrollToEnd(); await Task.Delay(100);
                    Assert(settingsScroll.VerticalOffset > 0, "connection category can scroll at minimum size");
                    model.ShowDashboardCommand.Execute(null); await Task.Delay(100);
                    Assert(contentScroll.VerticalOffset == 0, "dashboard navigation resets scroll");
                    model.LogsOpen = true; contentScroll.ScrollToEnd(); await Task.Delay(100);
                    model.ShowSettingsCommand.Execute(null); await Task.Delay(100);
                    Assert(settingsScroll.VerticalOffset == 0, "settings navigation resets category scroll");
                    var connectionTab = (TabItem)window.FindName("ConnectionTab");
                    connectionTab.Focus(); connectionTab.MoveFocus(new TraversalRequest(FocusNavigationDirection.Right));
                    Assert(((TabItem)window.FindName("AccessTab")).IsKeyboardFocused, "category tabs support arrow-key navigation");
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
                settingsTabs.SelectedItem = window.FindName("AccessTab"); window.UpdateLayout();
                emailsInput.BringIntoView(new Rect(0, 0, emailsInput.ActualWidth, emailsInput.ActualHeight + 60)); await Task.Delay(100);
                Render(window, Path.Combine(artifacts, "invalid-email-Light-860.png"), 96);
                settingsTabs.SelectedItem = window.FindName("ConnectionTab"); window.UpdateLayout();
                retryInput.BringIntoView(new Rect(0, 0, retryInput.ActualWidth, retryInput.ActualHeight + 60)); await Task.Delay(100);
                Render(window, Path.Combine(artifacts, "invalid-retry-Light-860.png"), 96);
                model.PortText = "8000"; model.RetryText = "3"; model.AllowedEmails = ""; model.IsAdvanced = false;
                foreach (var state in new[] { TunnelState.Starting, TunnelState.Stopping, TunnelState.Error })
                {
                    typeof(MainViewModel).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, new TunnelSnapshot(state, "상태별 동작 검증"));
                    typeof(MainViewModel).GetMethod("RefreshState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, null);
                    Assert(startButton.IsVisible == model.CanEdit && stopButton.IsVisible == !model.CanEdit, "one tunnel action is shown for " + state);
                    Assert(model.StopLabel == (state == TunnelState.Starting ? "연결 취소" : state == TunnelState.Stopping ? "중지하는 중…" : "터널 중지"), "state-specific stop label for " + state);
                    Assert(model.StopCommand.CanExecute(null) == (state == TunnelState.Starting), "stopping and error cannot trigger a duplicate stop");
                    AssertCaptionState(window, model);
                    model.IsAdvanced = true;
                    AssertRuntimeSettings(window, model);
                    model.IsAdvanced = false;
                }
                // Inject a service snapshot only in this test to verify bindings without publishing a fake tunnel.
                typeof(MainViewModel).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model,
                    new TunnelSnapshot(TunnelState.Connecting, "로컬 서버 대기 테스트", "https://test.trycloudflare.com", EdgeReady: true, LocalReady: false));
                typeof(MainViewModel).GetMethod("RefreshState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, null);
                Assert(model.StateLabel == "로컬 서버 대기" && model.HasUrl && model.CanStop && !model.IsConnecting, "offline origin preserves a usable tunnel URL and shows a waiting state");
                AssertCaptionState(window, model);
                Assert(!((FrameworkElement)window.FindName("ConnectionPanel")).IsVisible && !((FrameworkElement)window.FindName("SharePlaceholder")).IsVisible, "allocated URL focuses the dashboard on sharing");
                contentScroll.ScrollToTop(); await Task.Delay(150);
                AssertVisibleWithin((Button)window.FindName("CopyUrlButton"), contentScroll, "waiting server URL can be copied without scrolling at minimum size");
                await Task.Delay(250); Render(window, Path.Combine(artifacts, "waiting-for-server-Light.png"), 96);
                typeof(MainViewModel).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model,
                    new TunnelSnapshot(TunnelState.Connected, "연결 테스트", "https://a-very-long-readable-development-tunnel-address.trycloudflare.com", DateTimeOffset.UtcNow, Reachability: "HTTP 200 확인"));
                typeof(MainViewModel).GetMethod("RefreshState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(model, null);
                Assert(model.CopyUrlCommand.CanExecute(null), "URL command enablement");
                model.IsAdvanced = true; AssertRuntimeSettings(window, model);
                ((CheckBox)window.FindName("NotificationsInput")).IsChecked = false;
                ((CheckBox)window.FindName("StartInTrayInput")).IsChecked = true;
                Assert(!model.Notifications && model.StartInTray && model.SaveSettings(), "app options can be changed and saved while connected");
                var saved = new SettingsService(new AppPaths(root)).Load();
                Assert(!saved.Notifications && saved.StartInTray && saved.Port == 8000, "live app settings persist without changing the connection target");
                model.StartInTray = false; model.Notifications = true; model.IsAdvanced = false;
                model.DismissNoticeCommand.Execute(null);
                trayMenu.IsOpen = true; await Task.Delay(150);
                Assert(!((MenuItem)trayMenu.FindName("StartItem")).IsVisible && ((MenuItem)trayMenu.FindName("StopItem")).IsEnabled && ((MenuItem)trayMenu.FindName("CopyUrlItem")).IsEnabled, "connected tray menu exposes stop and sharing actions");
                Assert(((TextBlock)trayMenu.FindName("StatusLabel")).Text == model.StateLabel, "open tray menu receives live status bindings");
                RenderElement(trayMenu, trayMenu.Background, Path.Combine(artifacts, "tray-menu-connected-Light.png"), 96);
                trayMenu.IsOpen = false;
                RespondToExit(prompt => ((Button)prompt.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                model.ExitCommand.Execute(null);
                Assert(model.IsActive && model.HasUrl, "cancelling exit preserves the active tunnel and public URL");
                AssertCaptionState(window, model);
                Assert(stopButton.IsVisible && !startButton.IsVisible && model.StopLabel == "터널 중지", "connected state offers stop instead of a disabled start");
                contentScroll.ScrollToTop(); await Task.Delay(150);
                AssertVisibleWithin((Button)window.FindName("CopyUrlButton"), contentScroll, "long public URL and copy action fit the minimum window");
                await Task.Delay(250); Render(window, Path.Combine(artifacts, "connected-Light.png"), 96);
                window.Width = 1180; window.Height = 860; await Task.Delay(150);
                Render(window, Path.Combine(artifacts, "connected-Light-1180.png"), 96);
                app.Resources.MergedDictionaries[0] = Dictionary("Dark"); model.Theme = "Dark";
                AssertCaptionState(window, model);
                Render(window, Path.Combine(artifacts, "connected-Dark-1180.png"), 96);
                app.Resources.MergedDictionaries[0] = Dictionary("Light"); model.Theme = "Light";
                model.ShowQrCommand.Execute(null); await Task.Delay(250);
                Assert(app.Windows.OfType<QrWindow>().Any(), "QR window opens");
                var qrWindow = app.Windows.OfType<QrWindow>().First();
                Render(qrWindow, Path.Combine(artifacts, "qr-window-Light.png"), 96);
                qrWindow.Width = 360; qrWindow.Height = 480; await Task.Delay(100);
                AssertVisibleWithin((Button)qrWindow.FindName("CopyImageButton"), (FrameworkElement)qrWindow.Content, "QR copy action remains visible at minimum size");
                AssertVisibleWithin((Button)qrWindow.FindName("CloseButton"), (FrameworkElement)qrWindow.Content, "QR close action remains visible at minimum size");
                Render(qrWindow, Path.Combine(artifacts, "qr-window-Light-360.png"), 96);
                await CheckSmoothScroll(FindScrollViewer((DependencyObject)qrWindow.Content)!, "QR window");
                qrWindow.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(qrWindow), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert(!app.Windows.OfType<QrWindow>().Any(), "Escape closes the QR window");
                for (int i = 0; i < 10000; i++) model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "stress log " + i)]);
                Assert(model.Logs.Count == 2000, "UI log collection bounded at 2000");
                model.ShowLogsCommand.Execute(null); await Task.Delay(150);
                var fullList = (ListBox)window.FindName("FullActivityLog");
                Assert(model.IsLogs && window.IsVisible && fullList.IsVisible && app.Windows.Count == 1 && fullList.Items.Count == 2000, $"logs open inside the main window (logs {model.IsLogs}, window {window.IsVisible}/{window.WindowState}, list {fullList.IsVisible}/{fullList.Items.Count}, windows {app.Windows.Count})");
                Assert(((TextBox)window.FindName("LogSearchInput")).IsKeyboardFocused, "opening logs focuses the integrated search");
                model.Search = "9999";
                model.ShowDashboardCommand.Execute(null);
                model.ShowLogsCommand.Execute(null); await Task.Delay(100);
                Assert(model.IsLogs && model.Search == "9999" && fullList.Items.Count == 1 && app.Windows.Count == 1, "returning to logs preserves filters without creating a window");
                model.Search = "";
                model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "live integrated log update")]);
                Assert(((LogEntry)fullList.Items[fullList.Items.Count - 1]).Message == "live integrated log update", "new logs reach the integrated list immediately");
                foreach (var theme in new[] { "Light", "Dark" })
                {
                    app.Resources.MergedDictionaries[0] = Dictionary(theme); model.Theme = theme;
                    foreach (var size in new[] { (1180, 860), (860, 640) })
                    {
                        window.Width = size.Item1; window.Height = size.Item2; await Task.Delay(150);
                        AssertVisibleWithin((TextBox)window.FindName("LogSearchInput"), (FrameworkElement)window.Content, "integrated search fits at minimum window size");
                        AssertVisibleWithin((Button)window.FindName("LogsBackButton"), (FrameworkElement)window.Content, "integrated log actions stay visible");
                        Assert(!((FrameworkElement)window.FindName("LogPanel")).IsVisible, "full logs do not duplicate the recent preview");
                        Render(window, Path.Combine(artifacts, $"logs-integrated-{theme}-{size.Item1}.png"), 96);
                    }
                }
                model.Search = "9999"; Assert(model.FilteredLogs.Cast<LogEntry>().Count() == 1 && model.CopyLogsCommand.CanExecute(null) && model.SaveLogsCommand.CanExecute(null), "log search and export enablement");
                model.Search = ""; model.LevelFilter = "ERROR";
                Assert(model.FilteredLogs.IsEmpty && !model.HasFilteredLogs && !model.CopyLogsCommand.CanExecute(null) && !model.SaveLogsCommand.CanExecute(null), "empty filter disables exports");
                Assert(fullList.Items.Count == 0 && ((ListBox)window.FindName("ActivityLog")).Items.Count == 2000, "full-log filters do not change the recent preview");
                Render(window, Path.Combine(artifacts, "logs-empty-Dark.png"), 96);
                model.LevelFilter = "전체"; model.LogsOpen = true;
                await Task.Delay(200);
                Assert(Enumerable.Range(0, model.Logs.Count).Count(index => fullList.ItemContainerGenerator.ContainerFromIndex(index) is not null) < 100, "integrated logs remain virtualized");
                model.AutoScroll = false;
                var fullScroll = FindScrollViewer(fullList)!;
                await CheckSmoothScroll(fullScroll, "integrated logs");
                Assert(Enumerable.Range(0, model.Logs.Count).Count(index => fullList.ItemContainerGenerator.ContainerFromIndex(index) is not null) < 100, "smooth pixel scrolling preserves integrated log virtualization");
                model.AutoScroll = true; await Task.Delay(300);
                Assert(Math.Abs(fullScroll.VerticalOffset - fullScroll.ScrollableHeight) < 1, $"turning auto-follow back on reaches the latest entry (offset {fullScroll.VerticalOffset}, end {fullScroll.ScrollableHeight}, follow {((CloudflaredTunnelHost.Controls.LogListBox)fullList).AutoScroll}, animated {fullScroll.HasAnimatedProperties}, visible {fullList.IsVisible})");
                model.AutoScroll = false;
                fullScroll.ScrollToTop(); await Task.Delay(50);
                Wheel(fullScroll, -120);
                model.ShowDashboardCommand.Execute(null); await Task.Delay(100);
                Assert(!fullList.IsVisible && !fullScroll.HasAnimatedProperties, "leaving integrated logs stops hidden scroll animations");
                var list = (ListBox)window.FindName("ActivityLog");
                await CheckSmoothScroll(FindScrollViewer(list)!, "recent logs");
                model.ShowLogsCommand.Execute(null); model.AutoScroll = true;
                Wheel(fullScroll, -120);
                model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "auto-follow interrupts wheel animation")]);
                await Task.Delay(300);
                Assert(!fullScroll.HasAnimatedProperties && Math.Abs(fullScroll.VerticalOffset - fullScroll.ScrollableHeight) < 1, $"live log following reaches the latest entry (offset {fullScroll.VerticalOffset}, end {fullScroll.ScrollableHeight}, animated {fullScroll.HasAnimatedProperties}, logs {model.IsLogs}, visible {fullList.IsVisible}, window {window.WindowState})");
                Render(window, Path.Combine(artifacts, "logs-Dark.png"), 96);
                window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert(model.IsDashboard && window.IsVisible && app.Windows.Count == 1, "Escape returns from logs to the dashboard");
                model.ShowLogsCommand.Execute(null);
                Assert(model.IsLogs && app.Windows.Count == 1, "logs can reopen inside the main window");
                model.ClearLogsCommand.Execute(null);
                Assert(!model.HasFilteredLogs && !model.ClearLogsCommand.CanExecute(null), "clearing logs updates the empty state and command");
                AssertNativeNotifications(window, model);
                Assert(listener.Errors.Count == 0, "no WPF binding errors: " + string.Join("\n", listener.Errors));
                quittingViaModel = true;
                RespondToExit(prompt => ((Button)prompt.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                var tray = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                if (tray is not null)
                {
                    var menu = (ContextMenu)tray.GetType().GetField("_menu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
                    ((MenuItem)menu.FindName("ExitItem")).Command.Execute(null);
                }
                else model.ExitCommand.Execute(null);
                Console.WriteLine("PASS WPF themed tray menu, live app settings, exit confirmation/cancel/reentrancy, smooth scrolling, virtualization, custom title bar, validation, navigation, sharing, QR, logs, themes, DPI rendering, shutdown");
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
    private static void RespondToExit(Action<ExitConfirmationWindow> response) => Application.Current.Dispatcher.BeginInvoke(
        DispatcherPriority.ContextIdle, new Action(() => response(Application.Current.Windows.OfType<ExitConfirmationWindow>().Single())));
    private static void AssertRuntimeSettings(MainWindow window, MainViewModel model)
    {
        Assert(((StackPanel)window.FindName("ConnectionSettings")).IsEnabled == model.CanEdit, "connection options remain locked while a tunnel or startup job is active");
        Assert(((StackPanel)window.FindName("AccessSettings")).IsEnabled == model.CanEdit && ((ComboBox)window.FindName("LogLevelInput")).IsEnabled == model.CanEdit, "access and cloudflared logging options follow the connection lock");
        var tabs = (TabControl)window.FindName("SettingsTabs");
        Assert(tabs.Items.Cast<TabItem>().All(tab => tab.IsEnabled), "category navigation remains available during a tunnel");
        foreach (var name in new[] { "SaveLogFile", "StartTunnelOnLaunch", "StartInTray", "MinimizeToTray", "CloseToTray", "Notifications" })
            Assert(((CheckBox)window.FindName(name + "Input")).IsEnabled, name + " remains editable during startup and connection");
    }
    private static void AssertTunnelNotifications()
    {
        var method = typeof(TrayService).GetMethod("Notification", BindingFlags.Static | BindingFlags.NonPublic)!;
        (string Title, string Message)? Notice(TunnelSnapshot from, TunnelSnapshot to) => ((string, string)?)method.Invoke(null, [from, to]);
        var idle = new TunnelSnapshot(TunnelState.Idle, "");
        var starting = new TunnelSnapshot(TunnelState.Validating, "");
        var connecting = new TunnelSnapshot(TunnelState.Connecting, "", "https://test.trycloudflare.com");
        var waiting = connecting with { EdgeReady = true };
        var connected = waiting with { State = TunnelState.Connected, LocalReady = true };
        var reconnecting = new TunnelSnapshot(TunnelState.Reconnecting, "", Retry: 1);
        var error = new TunnelSnapshot(TunnelState.Error, "dev@example.com token=private https://test.trycloudflare.com");
        Assert(Notice(idle, idle) is null && Notice(idle, starting)?.Title == "터널 시작 중", "startup idle is quiet and a real start notifies");
        Assert(Notice(starting, connecting) is null, "URL allocation alone does not announce a successful tunnel");
        Assert(Notice(connecting, waiting)?.Title == "로컬 서버 대기" && Notice(waiting, connected)?.Title == "터널 연결 완료", "waiting origin and fully connected tunnel have distinct notifications");
        Assert(Notice(connected, waiting with { State = TunnelState.Reconnecting })?.Title == "로컬 서버 대기", "losing the local server does not claim the tunnel was closed");
        Assert(Notice(connected, reconnecting)?.Title == "터널 재연결 중" && Notice(reconnecting, connected)?.Title == "터널 연결 복구됨", "disconnect and recovery notify separately");
        Assert(Notice(reconnecting, reconnecting with { Retry = 2 }) is null && Notice(connected, connected with { Message = "refreshed" }) is null && Notice(waiting, waiting) is null, "polling and repeated retry status do not spam notifications");
        var stopping = connected with { State = TunnelState.Stopping };
        Assert(Notice(connected, stopping) is null && Notice(stopping, idle)?.Title == "터널 종료됨", "shutdown is announced after the tunnel has stopped");
        var failure = Notice(reconnecting, error);
        Assert(failure?.Title == "터널 연결 오류" && !failure.Value.Message.Contains("private") && !failure.Value.Message.Contains("example.com") && !failure.Value.Message.Contains("test.trycloudflare.com"), "notification error details use the existing privacy filter");
        Assert(Notice(error, error with { Message = "updated error" }) is null, "duplicate errors do not repeat notifications");
    }
    private static void AssertNativeNotifications(MainWindow window, MainViewModel model)
    {
        var tray = typeof(MainWindow).GetField("_tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
        if (tray is null) return;
        var type = tray.GetType();
        var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object Data() => type.GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
        uint Flags() => (uint)Data().GetType().GetField("Flags")!.GetValue(Data())!;
        var connected = new TunnelSnapshot(TunnelState.Connected, "", EdgeReady: true, LocalReady: true);
        model.Notifications = true;
        update.Invoke(tray, [connected]);
        Assert(Flags() == 16 && (string)Data().GetType().GetField("InfoTitle")!.GetValue(Data())! == "터널 연결 완료", "lifecycle notifications use the native Windows banner payload");
        update.Invoke(tray, [connected]);
        Assert(Flags() == 4, "repeated native status updates only refresh the tooltip");
        model.Notifications = false;
        var idle = new TunnelSnapshot(TunnelState.Idle, "");
        update.Invoke(tray, [idle]);
        Assert(Flags() == 4, "disabling notifications suppresses the native banner");
        model.Notifications = true;
        update.Invoke(tray, [idle]);
        Assert(Flags() == 4, "re-enabling notifications does not replay muted events");
        window.Hide();
        object[] args = [new WindowInteropHelper(window).Handle, 0x8001, (nint)0, (nint)0x405, false];
        type.GetMethod("Hook", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tray, args);
        Assert(window.IsVisible && (bool)args[4], "clicking a native notification restores the main window");
    }
    private static void AssertCaptionState(MainWindow window, MainViewModel model)
    {
        var indicator = (System.Windows.Shapes.Ellipse)window.FindName("CaptionStatusDot");
        var color = ((SolidColorBrush)window.FindResource(model.IsActive ? "SuccessBrush" : "MutedBrush")).Color;
        Assert(((SolidColorBrush)indicator.Fill).Color == color, "title bar indicator follows tunnel activity and theme");
        Assert(((TextBlock)window.FindName("CaptionStateLabel")).Text == model.StateLabel, "title bar describes the current connection state");
        Assert(WindowChrome.GetWindowChrome(window) is { CaptionHeight: 44, UseAeroCaptionButtons: false, GlassFrameThickness.Left: 1 }, "custom title bar retains a DWM frame");
        const int nativeFrame = 0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000;
        Assert((GetWindowLong(new WindowInteropHelper(window).Handle, -16) & nativeFrame) == nativeFrame, "native caption, resize, minimize and maximize styles remain available for Windows transitions");
        Assert(WindowFrame.GetIsEnabled(window) && WindowFrame.GetTheme(window) == model.Theme && !window.AllowsTransparency, "native frame follows the theme without a layered window");
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
    private static void Wheel(ScrollViewer viewer, int delta) => viewer.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
    private static async Task CheckSmoothScroll(ScrollViewer viewer, string name)
    {
        Assert(SmoothScroll.GetIsEnabled(viewer) && viewer.ScrollableHeight > 0, name + " enables smooth scrolling");
        if (SystemParameters.WheelScrollLines == 0) { Console.WriteLine("SKIP wheel animation: Windows wheel scrolling is disabled"); return; }
        viewer.ScrollToTop(); await Task.Delay(80);
        var distance = SystemParameters.WheelScrollLines < 0 ? viewer.ViewportHeight : 16d * SystemParameters.WheelScrollLines;
        var target = Math.Min(distance * 2, viewer.ScrollableHeight);
        var samples = new List<double>();
        ScrollChangedEventHandler sample = (_, _) => samples.Add(viewer.VerticalOffset);
        viewer.ScrollChanged += sample;
        Wheel(viewer, -120); Wheel(viewer, -120);
        if (SystemParameters.ClientAreaAnimation) Assert(viewer.HasAnimatedProperties, name + " animates wheel movement");
        await Task.Delay(350);
        viewer.ScrollChanged -= sample;
        if (SystemParameters.ClientAreaAnimation)
        {
            Assert(samples.Any(offset => offset > 0 && offset < target), name + " scrolls through intermediate offsets");
            Assert(Math.Abs(viewer.VerticalOffset - target) < 1, $"{name} accumulates rapid wheel input (actual {viewer.VerticalOffset}, target {target}, samples {string.Join(",", samples)})");
            viewer.ScrollToTop(); await Task.Delay(50);
            Wheel(viewer, -120); Wheel(viewer, -120); Wheel(viewer, 120);
            await Task.Delay(300);
            var reversed = Math.Clamp(target - distance, 0, viewer.ScrollableHeight);
            Assert(Math.Abs(viewer.VerticalOffset - reversed) < 1, $"{name} reverses pending wheel input (actual {viewer.VerticalOffset}, target {reversed})");
        }
        else Assert(!viewer.HasAnimatedProperties, name + " respects reduced motion");
        viewer.ScrollToBottom(); await Task.Delay(50);
        Wheel(viewer, -120); await Task.Delay(250);
        Assert(Math.Abs(viewer.VerticalOffset - viewer.ScrollableHeight) < 1, name + " stays within the lower boundary");
        viewer.ScrollToTop(); await Task.Delay(50);
    }
    private static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void AssertVisibleWithin(FrameworkElement element, FrameworkElement viewport, string message)
    {
        element.UpdateLayout(); viewport.UpdateLayout();
        var bounds = element.TransformToAncestor(viewport).TransformBounds(new Rect(element.RenderSize));
        Assert(element.IsVisible && bounds.Top >= 0 && bounds.Bottom <= viewport.ActualHeight && bounds.Left >= 0 && bounds.Right <= viewport.ActualWidth, $"{message} ({bounds} in {viewport.RenderSize}, visible {element.IsVisible}, window {Window.GetWindow(element)?.IsVisible})");
    }
    private static void Render(Window window, string path, double dpi)
    {
        RenderElement((FrameworkElement)window.Content, window.Background, path, dpi);
    }
    private static void RenderElement(FrameworkElement root, Brush backgroundBrush, string path, double dpi)
    {
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * dpi / 96), (int)Math.Ceiling(root.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        var offset = VisualTreeHelper.GetOffset(root);
        using (var drawing = background.RenderOpen())
        {
            drawing.DrawRectangle(backgroundBrush, null, new Rect(root.RenderSize));
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
