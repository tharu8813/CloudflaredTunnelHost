using System.Windows;
using System.Windows.Controls;
using CloudflaredTunnelHost.ViewModels;

namespace CloudflaredTunnelHost.Views;

public partial class TrayMenu : ContextMenu
{
    private readonly Action _show;
    public TrayMenu(MainViewModel model, Action show) { InitializeComponent(); DataContext = model; _show = show; }
    private void ShowMain_Click(object sender, RoutedEventArgs e) => _show();
}
