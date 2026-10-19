using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using CloudflaredTunnelHost.Controls;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.ViewModels;

namespace CloudflaredTunnelHost.Services;

public sealed class TrayService : IDisposable
{
    private const int Callback = 0x8001;
    private readonly HwndSource _source;
    private readonly MainViewModel _model;
    private readonly Action _show;
    private readonly ContextMenu _menu;
    private readonly nint _icon;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private bool _disposed;
    private readonly MenuItem _status;
    private NotifyIconData _data;

    public TrayService(Window window, MainViewModel model, Action show)
    {
        _model = model; _show = show;
        var handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("트레이 메시지 창을 만들 수 없습니다.");
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/CloudflaredTunnelHost;component/Assets/Branding/app.ico"))!.Stream;
        using var memory = new System.IO.MemoryStream(); stream.CopyTo(memory);
        var bytes = memory.ToArray();
        int count = BitConverter.ToUInt16(bytes, 4), entry = 6;
        for (int i = 0; i < count; i++) if (bytes[6 + i * 16] == 32) { entry = 6 + i * 16; break; }
        var length = BitConverter.ToInt32(bytes, entry + 8);
        var offset = BitConverter.ToInt32(bytes, entry + 12);
        _icon = CreateIconFromResourceEx(bytes.AsSpan(offset, length).ToArray(), (uint)length, true, 0x00030000, 32, 32, 0);
        if (_icon == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        _data = new NotifyIconData { Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = handle, Id = 1, Flags = 1 | 2 | 4, CallbackMessage = Callback, Icon = _icon, Tip = "Cloudflared Tunnel Host", Info = "", InfoTitle = "" };
        _source.AddHook(Hook);
        _status = new MenuItem { Header = model.StateLabel, IsEnabled = false };
        _menu = new ContextMenu { DataContext = model };
        _menu.Items.Add(_status);
        Add("메인 화면 표시", "dashboard", () => _show());
        _menu.Items.Add(new Separator());
        AddCommand("터널 시작", "play", model.StartCommand);
        AddCommand("터널 중지 / 작업 취소", "stop", model.StopCommand);
        AddCommand("URL 복사", "copy", model.CopyUrlCommand);
        AddCommand("브라우저 열기", "external", model.OpenUrlCommand);
        AddCommand("QR 코드 표시", "qr", model.ShowQrCommand);
        AddCommand("로그 폴더 열기", "terminal", model.OpenLogFolderCommand);
        _menu.Items.Add(new Separator());
        AddCommand("프로그램 완전 종료", "exit", model.ExitCommand);
        _model.StatusChanged += Update;
        try { AddIcon(); }
        catch { Dispose(); throw; }
    }
    private void AddIcon()
    {
        _data.Flags = 1 | 2 | 4;
        if (!ShellNotifyIcon(0, ref _data)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "시스템 트레이 아이콘을 등록할 수 없습니다.");
        _data.TimeoutOrVersion = 4; ShellNotifyIcon(4, ref _data);
    }
    private void Add(string label, string icon, Action action)
    {
        var item = new MenuItem { Header = label, Icon = MakeIcon(icon) };
        item.Click += (_, _) => action(); _menu.Items.Add(item);
    }
    private void AddCommand(string label, string icon, System.Windows.Input.ICommand command) => _menu.Items.Add(new MenuItem { Header = label, Icon = MakeIcon(icon), Command = command });
    private static SvgIcon MakeIcon(string kind)
    {
        var icon = new SvgIcon { Kind = kind, Width = 16, Height = 16 }; icon.SetResourceReference(SvgIcon.StrokeProperty, "TextBrush"); return icon;
    }
    private void Update(TunnelSnapshot snapshot)
    {
        if (_disposed) return;
        _data.Tip = $"Cloudflared Tunnel Host / {_model.StateLabel}";
        _data.Flags = 4; ShellNotifyIcon(1, ref _data);
        _status.Header = $"{_model.StateLabel} / {_model.Uptime}";
        if (_model.Notifications && snapshot.State is TunnelState.Error or TunnelState.Idle)
        {
            _data.Flags = 16; _data.InfoTitle = "Cloudflared Tunnel Host";
            _data.Info = snapshot.Message.Length > 255 ? snapshot.Message[..255] : snapshot.Message;
            _data.InfoFlags = 4; _data.BalloonIcon = _icon; ShellNotifyIcon(1, ref _data);
        }
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == _taskbarCreated && !_disposed)
        {
            try { AddIcon(); } catch (Exception ex) { _model.ReportError(ex); }
        }
        if (message != Callback || _disposed) return 0;
        int action = (int)((long)lParam & 0xffff);
        if (action is 0x203 or 0x400 or 0x401) { _show(); handled = true; }
        if (action is 0x205 or 0x7b)
        {
            SetForegroundWindow(hwnd);
            _status.Header = $"{_model.StateLabel} / {_model.Uptime}";
            _menu.Placement = PlacementMode.MousePoint; _menu.IsOpen = true; handled = true;
        }
        return 0;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _model.StatusChanged -= Update;
        _menu.IsOpen = false; ShellNotifyIcon(2, ref _data); _source.RemoveHook(Hook); DestroyIcon(_icon);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public nint Window; public uint Id; public uint Flags; public uint CallbackMessage; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State; public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint CreateIconFromResourceEx(byte[] data, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
}
