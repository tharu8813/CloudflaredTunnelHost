using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

public static class Motion
{
    public static readonly DependencyProperty WatchProperty = DependencyProperty.RegisterAttached("Watch", typeof(object), typeof(Motion), new PropertyMetadata(null, Changed));
    public static object GetWatch(DependencyObject element) => element.GetValue(WatchProperty);
    public static void SetWatch(DependencyObject element, object value) => element.SetValue(WatchProperty, value);
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || !SystemParameters.ClientAreaAnimation) return;
        var duration = (Duration)Application.Current.FindResource("MotionDuration");
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.35, 1, duration));
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }
}
