using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CloudflaredTunnelHost.Core.Models;

namespace CloudflaredTunnelHost.Infrastructure;

public sealed class LogCollection : ObservableCollection<LogEntry>
{
    public void AppendBatch(IReadOnlyList<LogEntry> entries)
    {
        foreach (var entry in entries) Items.Add(entry);
        while (Items.Count > 2000) Items.RemoveAt(0);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
