using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.Core.Services;
using CloudflaredTunnelHost.Infrastructure;
using CloudflaredTunnelHost.Services;
using Microsoft.Win32;

namespace CloudflaredTunnelHost.ViewModels;

public sealed class MainViewModel : ObservableObject, IDataErrorInfo
{
    private readonly AppPaths _paths;
    private readonly SettingsService _storage;
    private readonly LoggingService _logs;
    private readonly DownloadService _download;
    private readonly CloudflaredService _tunnel;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private Task _maintenance = Task.CompletedTask;
    private AppSettings _settings;
    private TunnelSnapshot _snapshot;
    private CloudflaredRelease? _release;
    private string _port;
    private string _retry;
    private string _notice = "";
    private string _version = "설치되지 않음";
    private string _search = "";
    private string _levelFilter = "전체";
    private bool _working;
    private double _progress;
    private enum Page { Dashboard, Settings, Logs }
    private Page _page;
    private bool _logsOpen = true;
    private bool _autoScroll = true;
    private BitmapSource? _qr;
    private DateTimeOffset _noticeUntil;
    public event Action? ThemeChanged;
    public event Action? ExitRequested;
    public event Action<TunnelSnapshot>? StatusChanged;
    public event Action? ShowQrRequested;
    public event Action? ShowLogsRequested;
    public LogCollection Logs { get; } = new();
    public ICollectionView FilteredLogs { get; }
    public string[] ServiceTypes { get; } = ["HTTP", "HTTPS"];
    public string[] Protocols { get; } = ["auto", "quic", "http2"];
    public string[] LogLevels { get; } = ["debug", "info", "warn", "error", "fatal"];
    public string[] FilterLevels { get; } = ["전체", "DEBUG", "INFO", "WARN", "ERROR", "FATAL"];

    public MainViewModel(AppPaths paths)
    {
        _paths = paths; _dispatcher = Dispatcher.CurrentDispatcher;
        _storage = new(paths); _settings = _storage.Load();
        _port = _settings.Port.ToString(); _retry = _settings.ReconnectLimit.ToString();
        _logs = new(paths) { SaveToFile = _settings.SaveLogFile };
        _download = new(paths); _tunnel = new(paths, _logs); _snapshot = _tunnel.Snapshot;
        _tunnel.Changed += TunnelChanged;
        // Popup filters must not change the default view used by the main log preview.
        FilteredLogs = new ListCollectionView(Logs);
        FilteredLogs.Filter = value => value is LogEntry entry && (LevelFilter == "전체" || entry.Level == LevelFilter)
            && (entry.Message.Contains(Search, StringComparison.CurrentCultureIgnoreCase) || entry.Level.Contains(Search, StringComparison.OrdinalIgnoreCase));
        FilteredLogs.CollectionChanged += (_, _) => { Raise(nameof(HasFilteredLogs)); CommandManager.InvalidateRequerySuggested(); };
        StartCommand = new AsyncCommand(() => MaintenanceAsync(StartAsync), ReportError, () => CanEdit && !HasInputErrors);
        StopCommand = new AsyncCommand(StopAsync, ReportError, () => CanStop);
        SaveSettingsCommand = new RelayCommand(() => SaveSettings(), () => !HasInputErrors);
        CheckUpdateCommand = new AsyncCommand(() => MaintenanceAsync(CheckUpdateAsync), ReportError, () => CanEdit);
        InstallCommand = new AsyncCommand(() => MaintenanceAsync(InstallAsync), ReportError, () => CanEdit);
        CopyUrlCommand = new RelayCommand(() => Guard(() => { DesktopService.Copy(PublicUrl); Notify("공개 주소를 복사했습니다."); }), () => HasUrl);
        OpenUrlCommand = new RelayCommand(() => Guard(() => DesktopService.Open(PublicUrl)), () => HasUrl);
        ShowQrCommand = new RelayCommand(() => Guard(() => { QrImage = DesktopService.MakeQr(PublicUrl); ShowQrRequested?.Invoke(); }), () => HasUrl);
        CopyQrCommand = new RelayCommand(() => Guard(() => { Clipboard.SetImage(QrImage!); Notify("QR 이미지를 복사했습니다."); }), () => HasUrl && QrImage is not null);
        SaveQrCommand = new RelayCommand(() => Guard(() => DesktopService.SaveQr(QrImage!)), () => HasUrl && QrImage is not null);
        SaveLogsCommand = new AsyncCommand(SaveLogsAsync, ReportError, () => HasFilteredLogs);
        CopyLogsCommand = new RelayCommand(() => Guard(() => { DesktopService.Copy(LogText()); Notify("필터링된 로그를 복사했습니다. 민감한 정보를 확인한 후 공유하세요."); }), () => HasFilteredLogs);
        ClearLogsCommand = new RelayCommand(() => { _logs.Clear(); Logs.Clear(); Raise(nameof(LogCount)); }, () => LogCount > 0);
        OpenLogFolderCommand = new RelayCommand(() => Guard(() => { Directory.CreateDirectory(_paths.Logs); DesktopService.Open(_paths.Logs); }));
        ToggleThemeCommand = new RelayCommand(() => Theme = Theme == "Dark" ? "Light" : "Dark");
        ShowDashboardCommand = new RelayCommand(() => ShowPage(Page.Dashboard));
        ShowSettingsCommand = new RelayCommand(() => IsAdvanced = true);
        ShowLogsCommand = new RelayCommand(() => { ShowPage(Page.Logs); ShowLogsRequested?.Invoke(); });
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());
        DismissNoticeCommand = new RelayCommand(() => Notice = "");
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Tick(), _dispatcher);
        _timer.Start();
        if (_storage.Notice is { } notice) Notify(notice, true);
        _logs.Write("Cloudflared Tunnel Host WPF를 시작했습니다.");
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand ShowQrCommand { get; }
    public ICommand CopyQrCommand { get; }
    public ICommand SaveQrCommand { get; }
    public ICommand SaveLogsCommand { get; }
    public ICommand CopyLogsCommand { get; }
    public ICommand ClearLogsCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand ShowLogsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand DismissNoticeCommand { get; }

    public AppSettings Settings => _settings;
    public TunnelState State => _snapshot.State;
    public string StateLabel => _snapshot.EdgeReady && !_snapshot.LocalReady ? "로컬 서버 대기" : _snapshot.EdgeReady && State != TunnelState.Connected ? "공개 주소 확인 중" : State switch
    {
        TunnelState.Idle => "대기 중", TunnelState.Validating => "로컬 서버 확인", TunnelState.Starting => "실행 준비",
        TunnelState.Connecting => "Cloudflare 연결 중", TunnelState.Connected => "연결됨", TunnelState.Reconnecting => "재연결 중", TunnelState.Stopping => "종료 중", _ => "연결 오류"
    };
    public string StatusIcon => State switch { TunnelState.Connected => "check", TunnelState.Error => "alert", TunnelState.Idle => "network", TunnelState.Stopping => "stop", _ => "activity" };
    public string StatusMessage => _snapshot.Message;
    public string Reachability => _snapshot.Reachability;
    public string PublicUrl => _snapshot.PublicUrl ?? "";
    public bool HasUrl => !string.IsNullOrWhiteSpace(_snapshot.PublicUrl) && _snapshot.IsActive && State != TunnelState.Stopping;
    public string UrlTitle => HasUrl ? "공개 주소" : "연결하면 공개 주소가 나타납니다";
    public string UrlDescription => State == TunnelState.Connected ? "이 주소로 로컬 서비스에 접속할 수 있습니다."
        : HasUrl && _snapshot.EdgeReady && !_snapshot.LocalReady ? "터널 주소가 준비되었습니다. 로컬 서버를 실행하면 같은 주소로 연결됩니다."
        : HasUrl ? "주소가 발급되었습니다. 공개 연결을 확인하고 있습니다." : "터널을 시작하면 공개 주소를 먼저 준비할 수 있습니다.";
    public bool IsActive => _snapshot.IsActive;
    public bool IsWorking { get => _working; private set { Set(ref _working, value); RefreshState(); } }
    public bool CanEdit => !IsActive && !IsWorking;
    public bool CanStop => (IsActive || IsWorking) && State != TunnelState.Stopping;
    public bool IsConnecting => !_snapshot.EdgeReady && (State is TunnelState.Validating or TunnelState.Starting or TunnelState.Connecting or TunnelState.Reconnecting);
    public bool AnimateProgress => IsConnecting && SystemParameters.ClientAreaAnimation;
    public string StopLabel => State == TunnelState.Stopping ? "중지하는 중…" : IsWorking ? "작업 취소" : IsConnecting ? "연결 취소" : "터널 중지";
    public string ActionHint => IsAdvanced ? "터널 시작 시에도 설정이 자동 저장됩니다."
        : CanEdit ? "첫 실행에 필요한 cloudflared는 자동으로 설치합니다."
        : "연결할 서버를 바꾸려면 먼저 터널을 중지하세요.";
    public string Uptime
    {
        get
        {
            var elapsed = _snapshot.ConnectedAt is { } at ? DateTimeOffset.UtcNow - at : TimeSpan.Zero;
            return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }
    }
    public string AttemptText => _snapshot.Retry == 0 ? "첫 연결" : $"재시도 {_snapshot.Retry}";
    public string LocalPreview => $"{ServiceType.ToLowerInvariant()}://localhost:{PortText}";
    public string AccessDescription => string.IsNullOrWhiteSpace(AllowedEmails) ? "주소를 아는 누구나 접근할 수 있습니다." : "허용된 이메일의 일회용 PIN 인증을 요구합니다.";
    public string Notice { get => _notice; private set { Set(ref _notice, value); Raise(nameof(HasNotice)); } }
    public bool HasNotice => Notice.Length > 0;
    public string Version { get => _version; private set => Set(ref _version, value); }
    public string ReleaseText => _release is null ? "업데이트를 확인하면 공식 최신 버전을 표시합니다." : $"공식 최신 버전: {_release.Version} / SHA256 검증 후 설치";
    public bool SupportsEmailAuth { get; private set; }
    public string EmailSupportText => SupportsEmailAuth ? "설치된 CLI에서 --allowed-mail 지원을 확인했습니다." : "설치된 CLI의 지원 여부를 확인해야 합니다. 지원하지 않으면 실행을 차단합니다.";
    public double DownloadProgress { get => _progress; private set => Set(ref _progress, value); }
    public string WorkText { get; private set; } = "";
    public bool IsAdvanced { get => _page == Page.Settings; set => ShowPage(value ? Page.Settings : Page.Dashboard); }
    public bool IsDashboard => _page == Page.Dashboard;
    public bool IsLogs => _page == Page.Logs;
    public int PageIndex => (int)_page;
    private void ShowPage(Page page)
    {
        if (_page == page) return;
        _page = page;
        Raise(nameof(PageIndex));
        Raise(nameof(IsAdvanced)); Raise(nameof(IsDashboard)); Raise(nameof(IsLogs)); Raise(nameof(ActionHint));
    }
    public bool LogsOpen { get => _logsOpen; set => Set(ref _logsOpen, value); }
    public bool AutoScroll { get => _autoScroll; set => Set(ref _autoScroll, value); }
    public string Search { get => _search; set { Set(ref _search, value); FilteredLogs.Refresh(); } }
    public string LevelFilter { get => _levelFilter; set { Set(ref _levelFilter, value); FilteredLogs.Refresh(); } }
    public int LogCount => Logs.Count;
    public bool HasFilteredLogs => !FilteredLogs.IsEmpty;
    public BitmapSource? QrImage { get => _qr; private set => Set(ref _qr, value); }

    public string PortText { get => _port; set { if (Set(ref _port, value)) { Raise(nameof(LocalPreview)); RefreshValidation(); } } }
    public string RetryText { get => _retry; set { if (Set(ref _retry, value)) RefreshValidation(); } }
    public string PortError => int.TryParse(PortText, out var port) && port is >= 1 and <= 65535 ? "" : "포트는 1부터 65535 사이의 숫자로 입력하세요.";
    public string RetryError => int.TryParse(RetryText, out var retry) && retry is >= 0 and <= 10 ? "" : "재연결 횟수는 0부터 10 사이의 숫자로 입력하세요.";
    public string EmailError
    {
        get
        {
            try { _settings.Validate(); return ""; }
            catch (ArgumentException ex) { return ex.Message; }
        }
    }
    public bool HasInputErrors => PortError.Length > 0 || RetryError.Length > 0 || EmailError.Length > 0;
    public string InputError => PortError.Length > 0 ? PortError : EmailError.Length > 0 ? EmailError : RetryError;
    public string Error => "";
    public string this[string columnName] => columnName switch { nameof(PortText) => PortError, nameof(RetryText) => RetryError, nameof(AllowedEmails) => EmailError, _ => "" };
    private void RefreshValidation()
    {
        Raise(nameof(PortError)); Raise(nameof(RetryError)); Raise(nameof(EmailError)); Raise(nameof(HasInputErrors)); Raise(nameof(InputError));
        CommandManager.InvalidateRequerySuggested();
    }
    private void ChangeSettings(AppSettings settings, string name) { _settings = settings; Raise(name); }
    public string ServiceType { get => _settings.ServiceType; set { ChangeSettings(_settings with { ServiceType = value }, nameof(ServiceType)); Raise(nameof(LocalPreview)); Raise(nameof(IsHttps)); } }
    public bool IsHttps => ServiceType == "HTTPS";
    public string LogLevel { get => _settings.LogLevel; set => ChangeSettings(_settings with { LogLevel = value }, nameof(LogLevel)); }
    public string Protocol { get => _settings.Protocol; set => ChangeSettings(_settings with { Protocol = value }, nameof(Protocol)); }
    public bool NoTlsVerify { get => _settings.NoTlsVerify; set => ChangeSettings(_settings with { NoTlsVerify = value }, nameof(NoTlsVerify)); }
    public bool SaveLogFile { get => _settings.SaveLogFile; set { ChangeSettings(_settings with { SaveLogFile = value }, nameof(SaveLogFile)); _logs.SaveToFile = value; } }
    public bool AutoReconnect { get => _settings.AutoReconnect; set => ChangeSettings(_settings with { AutoReconnect = value }, nameof(AutoReconnect)); }
    public bool StartTunnelOnLaunch { get => _settings.StartTunnelOnLaunch; set => ChangeSettings(_settings with { StartTunnelOnLaunch = value }, nameof(StartTunnelOnLaunch)); }
    public bool StartInTray { get => _settings.StartInTray; set => ChangeSettings(_settings with { StartInTray = value }, nameof(StartInTray)); }
    public bool MinimizeToTray { get => _settings.MinimizeToTray; set => ChangeSettings(_settings with { MinimizeToTray = value }, nameof(MinimizeToTray)); }
    public bool CloseToTray { get => _settings.CloseToTray; set => ChangeSettings(_settings with { CloseToTray = value }, nameof(CloseToTray)); }
    public bool Notifications { get => _settings.Notifications; set => ChangeSettings(_settings with { Notifications = value }, nameof(Notifications)); }
    public string AllowedEmails { get => _settings.AllowedEmails; set { ChangeSettings(_settings with { AllowedEmails = value }, nameof(AllowedEmails)); Raise(nameof(AccessDescription)); RefreshValidation(); } }
    public string Theme { get => _settings.Theme; set { ChangeSettings(_settings with { Theme = value }, nameof(Theme)); ThemeChanged?.Invoke(); Raise(nameof(State)); } }

    public async Task InitializeAsync()
    {
        var startOnLaunch = StartTunnelOnLaunch;
        try { await RefreshVersionAsync(_lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch (Exception ex) { _logs.Write(ex.Message, "WARN"); Version = "실행 파일 확인 실패 / 다시 설치 가능"; }
        if (!_lifetime.IsCancellationRequested && startOnLaunch) StartCommand.Execute(null);
    }
    private async Task RefreshVersionAsync(CancellationToken token)
    {
        if (!File.Exists(_paths.Executable)) { Version = "설치되지 않음 / 첫 실행 시 자동 설치"; return; }
        Version = await TunnelProcessManager.InspectAsync(_paths.Executable, ["--version"], token);
        if (!Version.StartsWith("cloudflared version", StringComparison.OrdinalIgnoreCase)) throw new IOException("실행 파일의 버전을 확인할 수 없습니다.");
        var help = await TunnelProcessManager.InspectAsync(_paths.Executable, ["tunnel", "--help"], token);
        SupportsEmailAuth = help.Contains("--allowed-mail", StringComparison.Ordinal); Raise(nameof(EmailSupportText));
    }
    private AppSettings ReadSettings()
    {
        if (!int.TryParse(PortText, out var port)) throw new ArgumentException("포트에는 1부터 65535 사이의 숫자를 입력하세요.");
        if (!int.TryParse(RetryText, out var retry)) throw new ArgumentException("재연결 횟수에는 0부터 10 사이의 숫자를 입력하세요.");
        var settings = _settings with { Port = port, ReconnectLimit = retry };
        settings.Validate(); return settings;
    }
    public bool SaveSettings()
    {
        try { _settings = ReadSettings(); _storage.Save(_settings); Notify("설정을 저장했습니다."); return true; }
        catch (Exception ex) { ReportError(ex); return false; }
    }
    private Task MaintenanceAsync(Func<CancellationToken, Task> work)
    {
        if (IsWorking || _lifetime.IsCancellationRequested) return Task.CompletedTask;
        _maintenance = RunAsync(); return _maintenance;
        async Task RunAsync()
        {
            IsWorking = true;
            _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            try { await work(_operation.Token); }
            catch (OperationCanceledException) when (_operation.IsCancellationRequested) { Notify("작업을 취소했습니다. 기존 실행 파일은 보존됩니다."); }
            finally { _operation.Dispose(); _operation = null; IsWorking = false; }
        }
    }
    private async Task StartAsync(CancellationToken token)
    {
        var settings = ReadSettings();
        _storage.Save(settings); _settings = settings;
        if (!File.Exists(_paths.Executable)) await InstallAsync(token);
        token.ThrowIfCancellationRequested();
        _tunnel.Start(settings);
    }
    private async Task StopAsync()
    {
        _operation?.Cancel();
        await _tunnel.StopAsync();
    }
    private void SetWork(string text) { WorkText = text; Raise(nameof(WorkText)); }
    private async Task CheckUpdateAsync(CancellationToken token)
    {
        SetWork("공식 최신 릴리스를 확인합니다.");
        _release = await _download.CheckUpdateAsync(token); Raise(nameof(ReleaseText)); Notify(ReleaseText);
    }
    private async Task InstallAsync(CancellationToken token)
    {
        await CheckUpdateAsync(token);
        SetWork("cloudflared를 다운로드하고 검증합니다."); DownloadProgress = 0;
        await _download.InstallAsync(_release!, new Progress<double>(value => DownloadProgress = value), token);
        await RefreshVersionAsync(token); Notify("cloudflared 설치와 버전 확인을 완료했습니다.");
    }
    private void TunnelChanged(TunnelSnapshot snapshot) => _dispatcher.BeginInvoke(() =>
    {
        var old = _snapshot; _snapshot = snapshot;
        if (old.PublicUrl != snapshot.PublicUrl || !HasUrl) QrImage = null;
        RefreshState();
        if (old.State != snapshot.State || old.EdgeReady != snapshot.EdgeReady || old.LocalReady != snapshot.LocalReady || old.PublicUrl != snapshot.PublicUrl) StatusChanged?.Invoke(snapshot);
    });
    private void RefreshState()
    {
        foreach (var name in new[] { nameof(State), nameof(StateLabel), nameof(StatusIcon), nameof(StatusMessage), nameof(Reachability), nameof(PublicUrl), nameof(HasUrl), nameof(UrlTitle), nameof(UrlDescription), nameof(IsActive), nameof(CanEdit), nameof(CanStop), nameof(IsConnecting), nameof(AnimateProgress), nameof(StopLabel), nameof(ActionHint), nameof(Uptime), nameof(AttemptText) }) Raise(name);
        CommandManager.InvalidateRequerySuggested();
    }
    private void Tick()
    {
        var batch = _logs.Drain();
        if (batch.Count > 0)
        {
            Logs.AppendBatch(batch);
            Raise(nameof(LogCount));
        }
        if (_logs.FileError is { } error && SaveLogFile) { SaveLogFile = false; Notify("로그 파일 저장이 중단되었습니다: " + error, true); }
        Raise(nameof(Uptime));
        if (_noticeUntil != default && DateTimeOffset.UtcNow > _noticeUntil) Notice = "";
    }
    private string LogText() => string.Join(Environment.NewLine, FilteredLogs.Cast<LogEntry>());
    private async Task SaveLogsAsync()
    {
        var dialog = new SaveFileDialog { Filter = "텍스트 로그|*.txt", FileName = $"tunnel-{DateTime.Now:yyyyMMdd-HHmmss}.txt", Title = "로그 저장 / 공유 전 민감한 정보를 확인하세요" };
        if (dialog.ShowDialog() != true) return;
        await File.WriteAllTextAsync(dialog.FileName, LogText()); Notify("필터링된 로그를 저장했습니다.");
    }
    private void Guard(Action action) { try { action(); } catch (Exception ex) { ReportError(ex); } }
    public void ReportError(Exception ex)
    {
        _logs.Write(ex.ToString(), "ERROR");
        Notify(ex switch
        {
            ArgumentException => ex.Message,
            System.Net.Http.HttpRequestException => "다운로드 서버에 연결할 수 없습니다. 인터넷 연결 및 GitHub API 사용 제한을 확인한 후 다시 시도하세요.",
            IOException => ex.Message,
            _ => "작업을 완료할 수 없습니다. 파일 권한 또는 클립보드 사용 상태를 확인하세요. 자세한 내용은 로그를 확인하세요."
        }, true);
    }
    public void Notify(string message, bool persistent = false) { Notice = message; _noticeUntil = persistent ? default : DateTimeOffset.UtcNow.AddSeconds(7); }
    public async Task ShutdownAsync()
    {
        _lifetime.Cancel(); _operation?.Cancel();
        await _tunnel.DisposeAsync();
        try { await _maintenance; } catch (Exception ex) { _logs.Write(ex.Message, "ERROR"); }
        _timer.Stop(); _tunnel.Changed -= TunnelChanged;
        _lifetime.Dispose();
    }
}
