using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CloudflaredTunnelHost.Core.Models;

namespace CloudflaredTunnelHost.Infrastructure;

public sealed class StateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Application.Current.FindResource(value switch
    {
        TunnelState.Connected => "SuccessBrush", TunnelState.Error => "DangerBrush", TunnelState.Idle => "MutedBrush", _ => "AccentBrush"
    });
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
