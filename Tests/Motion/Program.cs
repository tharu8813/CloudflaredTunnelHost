using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CloudflaredTunnelHost.Infrastructure;
using CloudflaredTunnelHost.Controls;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(Dictionary("Dark"));
        app.Resources.MergedDictionaries.Add(Dictionary("Controls"));
        var result = 0;
        app.Startup += async (_, _) =>
        {
            try { await Check(); Console.WriteLine("PASS motion: stagger, interruption, transform/opacity preservation, input, popup and tab integration"); }
            catch (Exception ex) { Console.Error.WriteLine(ex); result = 1; }
            finally { app.Shutdown(result); }
        };
        app.Run();
        return result;
    }

    private static ResourceDictionary Dictionary(string name) => new() { Source = new Uri($"pack://application:,,,/CloudflaredTunnelHost;component/Themes/{name}.xaml") };
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static TranslateTransform Offset(FrameworkElement element) => ((TransformGroup)element.RenderTransform).Children.OfType<TranslateTransform>().Last();
    private static ScaleTransform Scale(FrameworkElement element) => ((TransformGroup)element.RenderTransform).Children.OfType<ScaleTransform>().Last();
    private static void SendMouse(UIElement element, RoutedEvent routedEvent) => element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent });

    private static async Task Check()
    {
        var rotated = new RotateTransform(2);
        var panel = new StackPanel { Margin = new Thickness(30) };
        var first = new Border { Height = 45, Background = Brushes.Coral, RenderTransform = rotated, RenderTransformOrigin = new Point(.2, .3) };
        first.SetBinding(UIElement.OpacityProperty, new Binding { Source = .7 });
        panel.Children.Add(first);
        for (var i = 0; i < 5; i++) panel.Children.Add(new Border { Height = 45, Margin = new Thickness(0, 8, 0, 0), Background = Brushes.CadetBlue });
        var last = (FrameworkElement)panel.Children[^1];
        Motion.SetWatch(panel, true); Motion.SetStagger(panel, true);
        Assert(first.RenderTransform == rotated && !first.HasAnimatedProperties, "unloaded elements must stay untouched");
        var window = new Window { Width = 600, Height = 650, Content = panel, ShowInTaskbar = false };
        window.Show();
        await Task.Delay(90);
        if (SystemParameters.ClientAreaAnimation)
        {
            Assert(first.Opacity > last.Opacity && last.Opacity < .1, "staggered elements must stay hidden until their turn");
            Assert(Offset(first).Y > 0 && Offset(first).Y < 16, "reveal should produce intermediate positions");
            Assert(((TransformGroup)first.RenderTransform).Children[0] == rotated, "reveal must preserve an existing transform");
            Assert(first.RenderTransformOrigin == new Point(.2, .3), "reveal must preserve the caller's transform origin");
        }
        await Task.Delay(700);
        Assert(Math.Abs(first.Opacity - .7) < .001 && !first.HasAnimatedProperties, "reveal must restore bound opacity and release clocks");
        Assert(BindingOperations.IsDataBound(first, UIElement.OpacityProperty), "reveal must preserve opacity bindings");
        if (SystemParameters.ClientAreaAnimation) Assert(Offset(first).Y == 0 && !Offset(first).HasAnimatedProperties, "reveal must settle without held transform clocks");

        for (var i = 0; i < 6; i++)
        {
            panel.Visibility = Visibility.Collapsed;
            Motion.SetWatch(panel, false);
            panel.Visibility = Visibility.Visible;
            Motion.SetWatch(panel, true);
            await Task.Delay(25);
        }
        panel.Visibility = Visibility.Collapsed;
        Assert(!first.HasAnimatedProperties && (!SystemParameters.ClientAreaAnimation || Offset(first).Y == 0), "hiding a page must cancel every staggered child");
        panel.Visibility = Visibility.Visible;
        await Task.Delay(700);
        if (SystemParameters.ClientAreaAnimation) Assert(((TransformGroup)first.RenderTransform).Children.Count == 3, "repeat navigation must not accumulate transforms");

        var button = new Button { Content = "Motion check", Width = 200, Margin = new Thickness(20) };
        window.Content = button;
        await Task.Delay(80);
        Assert(Motion.GetInteractive(button), "shared button style must enable interaction");
        Assert(!Motion.GetInteractive(new Button { Style = (Style)Application.Current.FindResource("CaptionButton") }), "caption buttons retain native hit areas");
        SendMouse(button, UIElement.PreviewMouseLeftButtonDownEvent);
        await Task.Delay(130);
        if (SystemParameters.ClientAreaAnimation) Assert(Scale(button).ScaleX < .99, $"press must compress the button (scale {Scale(button).ScaleX}, visible {button.IsVisible}, enabled {button.IsEnabled})");
        SendMouse(button, UIElement.PreviewMouseLeftButtonUpEvent);
        await Task.Delay(300);
        if (SystemParameters.ClientAreaAnimation) Assert(Scale(button).ScaleX >= 1 && !Scale(button).HasAnimatedProperties, "release must settle and remove clocks");
        for (var i = 0; i < 8; i++)
        {
            SendMouse(button, UIElement.PreviewMouseLeftButtonDownEvent); await Task.Delay(12);
            SendMouse(button, UIElement.PreviewMouseLeftButtonUpEvent); await Task.Delay(12);
        }
        button.IsEnabled = false;
        await Task.Delay(300);
        if (SystemParameters.ClientAreaAnimation) Assert(Scale(button).ScaleX == 1 && !Scale(button).HasAnimatedProperties, "disabling during rapid input must restore scale");

        var checkBox = new CheckBox { Content = "스크롤 영역 가장자리의 체크박스", Margin = new Thickness(0) };
        var checkPanel = new StackPanel(); checkPanel.Children.Add(checkBox);
        var checkViewport = new ScrollViewer { Content = checkPanel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        window.Content = checkViewport;
        await Task.Delay(100);
        var box = (FrameworkElement)checkBox.Template.FindName("Box", checkBox);
        var restingBounds = box.TransformToAncestor(checkViewport).TransformBounds(new Rect(box.RenderSize));
        checkBox.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
        SendMouse(checkBox, UIElement.PreviewMouseLeftButtonDownEvent);
        await Task.Delay(150);
        var activeBounds = box.TransformToAncestor(checkViewport).TransformBounds(new Rect(box.RenderSize));
        Assert(Math.Abs(activeBounds.Left - restingBounds.Left) < .01 && Math.Abs(activeBounds.Width - restingBounds.Width) < .01,
            "checkbox box must keep its position and size during pointer interaction at the viewport edge");
        Assert(activeBounds.Left >= 0 && activeBounds.Right <= checkViewport.ActualWidth, "checkbox box must remain inside the scroll viewport");
        SendMouse(checkBox, UIElement.PreviewMouseLeftButtonUpEvent);

        var tabs = new TabControl();
        var draft = new TextBox { Text = "Keep this input", VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12) };
        var firstTab = new Grid { Background = Brushes.CadetBlue }; firstTab.Children.Add(draft);
        tabs.Items.Add(new TabItem { Header = "One", Content = firstTab });
        tabs.Items.Add(new TabItem { Header = "Two", Content = new TextBox { Text = "Second panel", VerticalAlignment = VerticalAlignment.Top } });
        tabs.Items.Add(new TabItem { Header = "Three", Content = new TextBlock { Text = "Third panel" } });
        window.Content = tabs;
        await Task.Delay(550);
        tabs.SelectedIndex = 1;
        await Task.Delay(60);
        var host = (TransitionPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
        var incoming = (ContentPresenter)host.Children[0];
        var outgoing = (Image)host.Children[1];
        Assert(ReferenceEquals(incoming.Content, tabs.SelectedContent), "tab changes must show the selected live content");
        if (SystemParameters.ClientAreaAnimation)
        {
            Assert(((TranslateTransform)incoming.RenderTransform).X > 0 && ((TranslateTransform)outgoing.RenderTransform).X < 0,
                "moving to a later tab must bring content from the right and move the old frame left");
            Assert(outgoing.Source is not null && !outgoing.IsHitTestVisible, "the outgoing frame must remain visible without intercepting input");
            var pixel = new byte[4];
            ((BitmapSource)outgoing.Source!).CopyPixels(new Int32Rect(2, 2, 1, 1), pixel, 4, 0);
            var debugFrame = new PngBitmapEncoder(); debugFrame.Frames.Add(BitmapFrame.Create((BitmapSource)outgoing.Source));
            using (var output = File.Create(".artifacts/tab-outgoing.png")) debugFrame.Save(output);
            Assert(pixel[0] == Colors.CadetBlue.B && pixel[1] == Colors.CadetBlue.G && pixel[2] == Colors.CadetBlue.R && pixel[3] == 255,
                $"outgoing frames must use local coordinates without shifting by the tab header or page margin (BGRA {string.Join(',', pixel)})");
        }
        await Task.Delay(450);
        Assert(incoming.Opacity == 1 && !incoming.HasAnimatedProperties && outgoing.Source is null,
            "completed transitions must restore live content and release their snapshot");
        tabs.SelectedIndex = 0;
        await Task.Delay(60);
        if (SystemParameters.ClientAreaAnimation)
            Assert(((TranslateTransform)incoming.RenderTransform).X < 0 && ((TranslateTransform)outgoing.RenderTransform).X > 0,
                "moving to an earlier tab must reverse both directions");
        Assert(draft.Text == "Keep this input" && draft.Focus(), "returning to a tab must preserve its input and keyboard access");
        foreach (var index in new[] { 2, 1, 2, 0 }) { tabs.SelectedIndex = index; await Task.Delay(35); }
        await Task.Delay(450);
        Assert(ReferenceEquals(incoming.Content, firstTab) && incoming.Opacity == 1 && outgoing.Source is null && ((TranslateTransform)incoming.RenderTransform).X == 0,
            "rapid reversals must finish on the latest tab without stale frames or offsets");
        tabs.SelectedIndex = 1; await Task.Delay(50);
        tabs.Visibility = Visibility.Collapsed;
        Assert(outgoing.Source is null && !incoming.HasAnimatedProperties && !((TranslateTransform)incoming.RenderTransform).HasAnimatedProperties,
            "hiding the tabs must release transition animations and snapshots");
        var combo = new ComboBox { Width = 220, VerticalAlignment = VerticalAlignment.Top };
        combo.Items.Add("HTTP"); combo.Items.Add("HTTPS"); combo.SelectedIndex = 0;
        window.Content = combo; await Task.Delay(100);
        combo.IsDropDownOpen = true; await Task.Delay(60);
        var popup = (System.Windows.Controls.Primitives.Popup)combo.Template.FindName("PART_Popup", combo);
        Assert(Motion.GetReveal(popup.Child), "dropdown must use the reveal motion");
        var popupClosed = new TaskCompletionSource();
        popup.Closed += (_, _) => popupClosed.TrySetResult();
        combo.IsDropDownOpen = false;
        await popupClosed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(!popup.Child.HasAnimatedProperties, $"closed dropdown must cancel reveal (open {popup.IsOpen}, visible {popup.Child.IsVisible})");
        window.Close();
        await CapturePreview();
    }

    private static async Task CapturePreview()
    {
        var paths = new CloudflaredTunnelHost.Core.Services.AppPaths(Path.GetFullPath(".test-data/motion-preview"));
        var model = new CloudflaredTunnelHost.ViewModels.MainViewModel(paths);
        var window = new CloudflaredTunnelHost.Views.MainWindow(model) { ShowInTaskbar = false };
        window.Show();
        await Task.Delay(850);
        model.Logs.Clear();
        model.DismissNoticeCommand.Execute(null);
        model.Logs.AppendBatch([new(DateTimeOffset.Now, "INFO", "화면 전환과 입력 모션 미리보기")]);
        var frames = new GifBitmapEncoder();
        for (var page = 0; page < 3; page++)
        {
            if (page == 0) model.ShowSettingsCommand.Execute(null);
            if (page == 1) model.ShowLogsCommand.Execute(null);
            if (page == 2) model.ShowDashboardCommand.Execute(null);
            await CaptureFrames(frames);
        }
        Directory.CreateDirectory(".artifacts");
        using (var output = File.Create(".artifacts/motion-preview.gif")) frames.Save(output);
        model.ShowSettingsCommand.Execute(null); await Task.Delay(450);
        var settingsTabs = (TabControl)window.FindName("SettingsTabs");
        var tabFrames = new GifBitmapEncoder();
        foreach (var index in new[] { 1, 3, 1, 0 })
        {
            settingsTabs.SelectedIndex = index;
            await CaptureFrames(tabFrames);
        }
        using (var output = File.Create(".artifacts/tab-transitions.gif")) tabFrames.Save(output);
        await model.ShutdownAsync();
        if (typeof(CloudflaredTunnelHost.Views.MainWindow).GetField("_tray", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(window) is IDisposable tray) tray.Dispose();
        typeof(CloudflaredTunnelHost.Views.MainWindow).GetField("_allowClose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(window, true);
        window.Close();

        async Task CaptureFrames(GifBitmapEncoder encoder)
        {
            for (var i = 0; i < 16; i++)
            {
                await Task.Delay(i == 15 ? 400 : 40);
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var metadata = new BitmapMetadata("gif");
                metadata.SetQuery("/grctlext/Delay", (ushort)(i == 15 ? 65 : 5));
                encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
            }
        }
    }
}
