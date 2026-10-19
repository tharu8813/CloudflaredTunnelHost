using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using CloudflaredTunnelHost.Core.Models;
using CloudflaredTunnelHost.Core.Services;
using CloudflaredTunnelHost.ViewModels;
using CloudflaredTunnelHost.Views;

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
    private NotifyIconData _data;
    private TunnelSnapshot _previous = new(TunnelState.Idle, "");

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
        _menu = new TrayMenu(model, _show);
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
    private void Update(TunnelSnapshot snapshot)
    {
        if (_disposed) return;
        var notification = Notification(_previous, snapshot);
        _previous = snapshot;
        _data.Tip = $"Cloudflared Tunnel Host / {_model.StateLabel}";
        _data.Flags = 4; ShellNotifyIcon(1, ref _data);
        if (_model.Notifications && notification is { } info)
        {
            _data.Flags = 16; _data.InfoTitle = info.Title;
            _data.Info = info.Message.Length > 255 ? info.Message[..255] : info.Message;
            _data.InfoFlags = 4 | 0x80; _data.BalloonIcon = _icon; ShellNotifyIcon(1, ref _data);
        }
    }
    private static (string Title, string Message)? Notification(TunnelSnapshot previous, TunnelSnapshot current)
    {
        var wasWaiting = previous.EdgeReady && !previous.LocalReady && previous.PublicUrl is not null;
        var waiting = current.EdgeReady && !current.LocalReady && current.PublicUrl is not null;
        if (current.State == TunnelState.Error && previous.State != TunnelState.Error)
            return ("터널 연결 오류", LoggingService.Redact(current.Message));
        if (current.State == TunnelState.Idle && previous.State != TunnelState.Idle)
            return ("터널 종료됨", "터널 연결 작업이 종료되었습니다. 이전 공개 주소는 사용할 수 없습니다.");
        if (current.State == TunnelState.Validating && previous.State != TunnelState.Validating)
            return ("터널 시작 중", "로컬 서버를 확인하고 터널 연결을 준비합니다.");
        if (current.State == TunnelState.Connected && previous.State != TunnelState.Connected)
            return (previous.State == TunnelState.Reconnecting ? "터널 연결 복구됨" : "터널 연결 완료", "터널과 로컬 서버가 연결되었습니다. 앱에서 공개 주소를 복사하거나 공유하세요.");
        if (waiting && !wasWaiting && current.IsActive && current.State != TunnelState.Stopping)
            return ("로컬 서버 대기", "터널은 열려 있지만 로컬 서버에 연결할 수 없습니다. 서버를 실행하면 같은 공개 주소로 연결됩니다.");
        if (current.State == TunnelState.Reconnecting && !waiting && (previous.State != TunnelState.Reconnecting || wasWaiting))
            return ("터널 재연결 중", "연결 상태가 바뀌어 다시 연결을 확인하고 있습니다. 복구 결과를 다시 알려드립니다.");
        return null;
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == _taskbarCreated && !_disposed)
        {
            try { AddIcon(); } catch (Exception ex) { _model.ReportError(ex); }
        }
        if (message != Callback || _disposed) return 0;
        int action = (int)((long)lParam & 0xffff);
        if (action is 0x203 or 0x400 or 0x401 or 0x405) { _show(); handled = true; }
        if (action is 0x205 or 0x7b)
        {
            SetForegroundWindow(hwnd);
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
