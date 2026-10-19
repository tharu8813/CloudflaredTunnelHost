using System.Windows;
using CloudflaredTunnelHost.ViewModels;

namespace CloudflaredTunnelHost.Views;
public partial class QrWindow : Window
{
    public QrWindow(MainViewModel model) { InitializeComponent(); DataContext = model; }
}
