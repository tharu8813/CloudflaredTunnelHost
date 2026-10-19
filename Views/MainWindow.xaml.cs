using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using CloudflaredTunnelHost.Infrastructure;
using CloudflaredTunnelHost.Services;
using CloudflaredTunnelHost.ViewModels;

namespace CloudflaredTunnelHost.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _model;
    private TrayService? _tray;
    private QrWindow? _qr;
    private ExitConfirmationWindow? _exitPrompt;
    private bool _quitting;
    private bool _allowClose;
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); _model = model; DataContext = model;
        SourceInitialized += (_, _) =>
        {
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
        model.ShowQrRequested += () =>
        {
            if (_quitting || _allowClose) return;
            Restore(); _qr?.Close(); _qr = new QrWindow(model) { Owner = this }; _qr.Closed += (_, _) => _qr = null; _qr.Show();
        };
        model.ShowLogsRequested += () =>
        {
            if (_quitting || _allowClose) return;
            Restore(); LogSearchInput.Focus();
        };
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(model.QrImage) && model.QrImage is null) _qr?.Close();
            if (e.PropertyName == nameof(model.IsDashboard))
            {
                (model.IsLogs ? LogsNavigation : model.IsAdvanced ? SettingsNavigation : DashboardNavigation).Focus();
                SmoothScroll.Stop(ContentScroll);
                ContentScroll.UpdateLayout(); ContentScroll.ScrollToTop();
                ResetSettingsScroll();
            }
        };
        Application.Current.SessionEnding += HandleSessionEnding;
    }
    private void HideToTray() { if (_quitting || _allowClose) return; Hide(); ShowInTaskbar = false; }
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_model.IsLogs) return;
        _model.ShowDashboardCommand.Execute(null); e.Handled = true;
    }
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        // shortcut: this client button omits hover Snap Layouts; add non-client hit testing if required.
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
    private void ReviewInput_Click(object sender, RoutedEventArgs e)
    {
        _model.IsAdvanced = _model.PortError.Length == 0;
        if (_model.IsAdvanced) SettingsTabs.SelectedItem = _model.EmailError.Length > 0 ? AccessTab : ConnectionTab;
        var input = _model.PortError.Length > 0 ? PortInput : _model.EmailError.Length > 0 ? AllowedEmailsInput : RetryInput;
        SmoothScroll.Stop(ContentScroll);
        UpdateLayout();
        input.BringIntoView(); input.Focus(); input.SelectAll();
    }
    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == SettingsTabs) ResetSettingsScroll();
    }
    private void ResetSettingsScroll()
    {
        if (SettingsTabs.SelectedItem is not TabItem { Content: ScrollViewer scroll }) return;
        SmoothScroll.Stop(scroll); scroll.ScrollToTop();
    }
    private void Restore() { if (_quitting || _allowClose) return; ShowInTaskbar = true; Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_quitting) return;
        // WPF must finish the cancelled Closing event before we hide or close this window again.
        if (_model.CloseToTray && _tray is not null) Dispatcher.BeginInvoke(new Action(HideToTray));
        else Dispatcher.BeginInvoke(new Action(RequestExit));
    }
    private async void RequestExit()
    {
        if (_quitting || _allowClose) return;
        if (_exitPrompt is not null) { _exitPrompt.Activate(); return; }
        Restore();
        _exitPrompt = new ExitConfirmationWindow { Owner = this, DataContext = _model };
        bool confirmed;
        try { confirmed = _exitPrompt.ShowDialog() == true; }
        finally { _exitPrompt = null; }
        if (confirmed && !_quitting) await QuitAsync();
    }
    private async Task QuitAsync()
    {
        if (_quitting) return;
        _quitting = true; IsEnabled = false;
        try
        {
            _model.SaveSettings();
            await _model.ShutdownAsync();
            _qr?.Close(); _tray?.Dispose(); _allowClose = true;
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
        _quitting = true; _exitPrompt?.Close(); _tray?.Dispose();
        try { await _model.ShutdownAsync(); }
        catch (Exception ex) { _model.ReportError(ex); }
    }
}
