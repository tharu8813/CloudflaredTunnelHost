using System.Windows;
using System.Windows.Input;
using CloudflaredTunnelHost.ViewModels;

namespace CloudflaredTunnelHost.Views;

public partial class LogWindow : Window
{
    public LogWindow(MainViewModel model) { InitializeComponent(); DataContext = model; }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close(); e.Handled = true;
    }
}
