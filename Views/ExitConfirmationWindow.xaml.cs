using System.Windows;

namespace CloudflaredTunnelHost.Views;

public partial class ExitConfirmationWindow : Window
{
    public ExitConfirmationWindow() { InitializeComponent(); Loaded += (_, _) => CancelButton.Focus(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
