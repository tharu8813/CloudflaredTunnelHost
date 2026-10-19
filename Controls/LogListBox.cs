using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CloudflaredTunnelHost.Infrastructure;

namespace CloudflaredTunnelHost.Controls;

public sealed class LogListBox : ListBox
{
    private bool _scrollQueued;
    public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(LogListBox), new PropertyMetadata(true, (d, e) => { if ((bool)e.NewValue) ((LogListBox)d).QueueScroll(); }));
    public bool AutoScroll { get => (bool)GetValue(AutoScrollProperty); set => SetValue(AutoScrollProperty, value); }
    public LogListBox()
    {
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) QueueScroll(); };
        SizeChanged += (_, _) => QueueScroll();
    }
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        QueueScroll();
    }
    private void QueueScroll()
    {
        if (!AutoScroll || !IsVisible || _scrollQueued) return;
        _scrollQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _scrollQueued = false;
            if (!AutoScroll || !IsVisible || Items.Count == 0) return;
            // Cancel wheel movement before WPF queues the request to reveal the newest row.
            if (FindScrollViewer(this) is { } viewer) SmoothScroll.Stop(viewer);
            ScrollIntoView(Items[Items.Count - 1]);
        }));
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(element, i)) is { } child) return child;
        return null;
    }
}
