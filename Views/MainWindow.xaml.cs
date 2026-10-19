using System.ComponentModel;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CloudflaredTunnelHost.Services;
using CloudflaredTunnelHost.ViewModels;

namespace CloudflaredTunnelHost.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _model;
    private TrayService? _tray;
    private QrWindow? _qr;
    private bool _quitting;
    private bool _allowClose;
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); _model = model; DataContext = model;
        SourceInitialized += (_, _) =>
        {
            ApplyCaptionTheme();
            try { _tray = new TrayService(this, model, Restore); }
            catch (Exception ex) { model.ReportError(new IOException("시스템 트레이를 등록할 수 없습니다. 창을 유지합니다. " + ex.Message, ex)); }
        };
        Loaded += async (_, _) =>
        {
            if (model.StartInTray && _tray is not null) HideToTray();
            await model.InitializeAsync();
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && model.MinimizeToTray && _tray is not null && !_quitting) HideToTray(); };
        Closing += OnClosing;
        model.ExitRequested += RequestExit;
        model.ThemeChanged += ApplyCaptionTheme;
        model.ShowQrRequested += () =>
        {
            Restore(); _qr?.Close(); _qr = new QrWindow(model) { Owner = this }; _qr.Closed += (_, _) => _qr = null; _qr.Show();
        };
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(model.QrImage) && model.QrImage is null) _qr?.Close();
            if (e.PropertyName == nameof(model.IsAdvanced)) ContentScroll.ScrollToTop();
        };
        Application.Current.SessionEnding += HandleSessionEnding;
    }
    private void HideToTray() { Hide(); ShowInTaskbar = false; }
    private void Restore() { ShowInTaskbar = true; Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_quitting) return;
        if (_model.CloseToTray && _tray is not null) { HideToTray(); return; }
        await QuitAsync();
    }
    private async void RequestExit() => await QuitAsync();
    private async Task QuitAsync()
    {
        if (_quitting) return;
        _quitting = true; IsEnabled = false;
        try
        {
            _model.SaveSettings();
            await _model.ShutdownAsync();
            _qr?.Close(); _tray?.Dispose(); _allowClose = true;
            _model.ThemeChanged -= ApplyCaptionTheme;
            Application.Current.SessionEnding -= HandleSessionEnding;
            Close(); Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _quitting = false; IsEnabled = true; Restore(); _model.ReportError(ex);
        }
    }
    private async void HandleSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        // Do not cancel Windows logoff. The kill-on-close job also protects a forced OS shutdown.
        _quitting = true; _tray?.Dispose();
        try { await _model.ShutdownAsync(); }
        catch (Exception ex) { _model.ReportError(ex); }
    }
    private void ApplyCaptionTheme()
    {
        var dark = _model.Theme == "Dark" ? 1 : 0;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
