using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CloudflaredTunnelHost.Controls;

public sealed class LogListBox : ListBox
{
    private bool _scrollQueued;
    public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(LogListBox), new PropertyMetadata(true));
    public bool AutoScroll { get => (bool)GetValue(AutoScrollProperty); set => SetValue(AutoScrollProperty, value); }
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        if (!AutoScroll || _scrollQueued) return;
        _scrollQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _scrollQueued = false;
            if (AutoScroll && Items.Count > 0) ScrollIntoView(Items[Items.Count - 1]);
        }));
    }
}
