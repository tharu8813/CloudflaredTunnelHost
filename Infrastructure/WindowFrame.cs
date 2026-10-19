using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CloudflaredTunnelHost.Infrastructure;

public static class WindowFrame
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(WindowFrame), new PropertyMetadata(false, EnabledChanged));
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.RegisterAttached(
        "Theme", typeof(string), typeof(WindowFrame), new PropertyMetadata("Dark", (d, _) => Apply((Window)d)));
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static string GetTheme(DependencyObject element) => (string)element.GetValue(ThemeProperty);
    public static void SetTheme(DependencyObject element, string value) => element.SetValue(ThemeProperty, value);

    private static void EnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)d;
        if ((bool)e.NewValue) { window.SourceInitialized += Initialized; Apply(window); }
        else window.SourceInitialized -= Initialized;
    }
    private static void Initialized(object? sender, EventArgs e) => Apply((Window)sender!);
    private static void Apply(Window window)
    {
        if (!GetIsEnabled(window)) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        int transitionsDisabled = 0, corners = 2, dark = GetTheme(window) == "Dark" ? 1 : 0;
        DwmSetWindowAttribute(handle, 3, ref transitionsDisabled, sizeof(int));
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        // Windows 11 rounds the native frame; older Windows versions ignore this preference.
        DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
