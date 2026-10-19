using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CloudflaredTunnelHost.Infrastructure;

public static class SmoothScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScroll), new PropertyMetadata(false, EnabledChanged));
    private static readonly DependencyProperty AnimationProperty = DependencyProperty.RegisterAttached(
        "Animation", typeof(DoubleAnimation), typeof(SmoothScroll));
    private static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached(
        "Offset", typeof(double), typeof(SmoothScroll), new PropertyMetadata(0d,
            (d, e) => ((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void EnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer) return;
        if ((bool)e.NewValue)
        {
            viewer.PreviewMouseWheel += Wheel;
            viewer.PreviewMouseDown += Interrupt;
            viewer.PreviewKeyDown += Interrupt;
            viewer.RequestBringIntoView += Interrupt;
            viewer.Unloaded += Interrupt;
            viewer.IsVisibleChanged += VisibilityChanged;
        }
        else
        {
            viewer.PreviewMouseWheel -= Wheel;
            viewer.PreviewMouseDown -= Interrupt;
            viewer.PreviewKeyDown -= Interrupt;
            viewer.RequestBringIntoView -= Interrupt;
            viewer.Unloaded -= Interrupt;
            viewer.IsVisibleChanged -= VisibilityChanged;
            Stop(viewer);
        }
    }

    public static void Stop(ScrollViewer viewer)
    {
        if (viewer.GetValue(AnimationProperty) is null) return;
        viewer.ClearValue(AnimationProperty);
        viewer.SetValue(OffsetProperty, viewer.VerticalOffset);
        viewer.BeginAnimation(OffsetProperty, null);
    }

    private static void Interrupt(object sender, RoutedEventArgs e) => Stop((ScrollViewer)sender);
    private static void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) Stop((ScrollViewer)sender);
    }

    private static void Wheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        if (e.Handled || e.Delta == 0) return;
        if (!SystemParameters.ClientAreaAnimation) { Stop(viewer); return; }
        if (viewer.ScrollableHeight <= 0 || SystemParameters.WheelScrollLines == 0) return;
        // An outer viewer must leave scrolling to a nested list until that list reaches its edge.
        for (var element = e.OriginalSource as DependencyObject; element is not null && element != viewer;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is ScrollViewer inner && CanScroll(inner, e.Delta)) return;
        }

        var pending = (DoubleAnimation?)viewer.GetValue(AnimationProperty);
        var origin = pending?.To ?? viewer.VerticalOffset;
        var distance = SystemParameters.WheelScrollLines < 0 ? viewer.ViewportHeight : 16d * SystemParameters.WheelScrollLines;
        var target = Math.Clamp(origin - e.Delta / 120d * distance, 0, viewer.ScrollableHeight);
        if (target == viewer.VerticalOffset && pending is null) return;
        e.Handled = true;
        if (target == pending?.To) return;

        Stop(viewer);
        var animation = new DoubleAnimation(viewer.VerticalOffset, target, (Duration)Application.Current.FindResource("MotionDuration"))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        animation.Completed += (_, _) =>
        {
            // A removed animation can finish after another scroll or page change has taken over.
            if (!ReferenceEquals(viewer.GetValue(AnimationProperty), animation)) return;
            viewer.SetValue(OffsetProperty, target);
            viewer.BeginAnimation(OffsetProperty, null);
            viewer.ClearValue(AnimationProperty);
        };
        viewer.SetValue(AnimationProperty, animation);
        viewer.BeginAnimation(OffsetProperty, animation);
    }

    private static bool CanScroll(ScrollViewer viewer, int delta)
    {
        var offset = ((DoubleAnimation?)viewer.GetValue(AnimationProperty))?.To ?? viewer.VerticalOffset;
        return delta < 0 ? offset < viewer.ScrollableHeight : offset > 0;
    }
}
