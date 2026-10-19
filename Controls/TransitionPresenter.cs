using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CloudflaredTunnelHost.Infrastructure;

namespace CloudflaredTunnelHost.Controls;

[ContentProperty(nameof(Content))]
public sealed class TransitionPresenter : Grid
{
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(TransitionPresenter), new PropertyMetadata(null, ContentChanged));
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
        nameof(Index), typeof(int), typeof(TransitionPresenter), new PropertyMetadata(0, (d, _) => ((TransitionPresenter)d).Prepare()));
    public static readonly DependencyProperty HorizontalTransitionIndexProperty = DependencyProperty.Register(
        nameof(HorizontalTransitionIndex), typeof(int), typeof(TransitionPresenter), new PropertyMetadata(-1));
    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(
        nameof(TransitionDuration), typeof(int), typeof(TransitionPresenter), new PropertyMetadata(MotionProfile.PageDurationMs));
    public static readonly DependencyProperty TravelProperty = DependencyProperty.Register(
        nameof(Travel), typeof(double), typeof(TransitionPresenter), new PropertyMetadata(MotionProfile.PageTravel));
    public static readonly DependencyProperty ContentTemplateProperty = DependencyProperty.Register(
        nameof(ContentTemplate), typeof(DataTemplate), typeof(TransitionPresenter));
    public static readonly DependencyProperty ContentStringFormatProperty = DependencyProperty.Register(
        nameof(ContentStringFormat), typeof(string), typeof(TransitionPresenter));
    public static readonly DependencyProperty ContentTemplateSelectorProperty = DependencyProperty.Register(
        nameof(ContentTemplateSelector), typeof(DataTemplateSelector), typeof(TransitionPresenter));

    private readonly ContentPresenter _incoming = new();
    private readonly Image _outgoing = new() { IsHitTestVisible = false, Focusable = false, Stretch = Stretch.Fill, Visibility = Visibility.Collapsed };
    private readonly TranslateTransform _incomingOffset = new();
    private readonly TranslateTransform _outgoingOffset = new();
    private DispatcherOperation? _pending;
    private int _previousIndex;
    private int _revision;

    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    public int HorizontalTransitionIndex { get => (int)GetValue(HorizontalTransitionIndexProperty); set => SetValue(HorizontalTransitionIndexProperty, value); }
    public int TransitionDuration { get => (int)GetValue(TransitionDurationProperty); set => SetValue(TransitionDurationProperty, value); }
    public double Travel { get => (double)GetValue(TravelProperty); set => SetValue(TravelProperty, value); }
    public DataTemplate? ContentTemplate { get => (DataTemplate?)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
    public string? ContentStringFormat { get => (string?)GetValue(ContentStringFormatProperty); set => SetValue(ContentStringFormatProperty, value); }
    public DataTemplateSelector? ContentTemplateSelector { get => (DataTemplateSelector?)GetValue(ContentTemplateSelectorProperty); set => SetValue(ContentTemplateSelectorProperty, value); }

    public TransitionPresenter()
    {
        ClipToBounds = true;
        _incoming.RenderTransform = _incomingOffset;
        _outgoing.RenderTransform = _outgoingOffset;
        foreach (var property in new[] { ContentPresenter.ContentTemplateProperty, ContentPresenter.ContentStringFormatProperty, ContentPresenter.ContentTemplateSelectorProperty })
            _incoming.SetBinding(property, new Binding(property.Name) { Source = this });
        Children.Add(_incoming);
        Children.Add(_outgoing);
        Loaded += (_, _) => _previousIndex = Index;
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, e) => { if (!(bool)e.NewValue) Stop(); };
        // Page layout can resize mid-transition when contextual footer actions collapse.
        // Keep the transition running rather than snapping to its final state.
    }

    private static void ContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var presenter = (TransitionPresenter)d;
        presenter.Prepare();
        presenter._incoming.Content = e.NewValue;
    }

    private void Prepare()
    {
        if (!SystemParameters.ClientAreaAnimation || !IsLoaded || !IsVisible || ActualWidth < 1 || ActualHeight < 1 || _incoming.Content is null)
        {
            Stop();
            return;
        }
        if (_pending?.Status == DispatcherOperationStatus.Pending) return;

        // Capture the displayed frame, including an interrupted transition, before replacing live content.
        var dpi = VisualTreeHelper.GetDpi(this);
        var snapshot = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
            context.DrawRectangle(new VisualBrush(this) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(RenderSize) }, null, new Rect(RenderSize));
        snapshot.Render(drawing);
        snapshot.Freeze();
        var previousIndex = _previousIndex;
        Stop();
        _previousIndex = previousIndex;
        _outgoing.Source = snapshot;
        _outgoing.Visibility = Visibility.Visible;
        _incoming.Opacity = 0;
        // Content and SelectedIndex bindings can update in either order in the same dispatcher turn.
        _pending = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(Start));
    }

    private void Start()
    {
        _pending = null;
        if (!IsVisible || !SystemParameters.ClientAreaAnimation) { Stop(); return; }
        var direction = Index < _previousIndex ? -1 : 1;
        var horizontal = HorizontalTransitionIndex < 0 || Index == HorizontalTransitionIndex || _previousIndex == HorizontalTransitionIndex;
        _previousIndex = Index;
        // A single restrained transition belongs to the content presenter.
        var axis = horizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty;
        var duration = Math.Clamp(TransitionDuration, 0, 800);
        var distance = direction * Math.Clamp(Travel, 0, 48);
        if (duration == 0) { Stop(); return; }
        var revision = _revision;
        Animate(_outgoingOffset, axis, 0, -distance * .3, duration);
        Animate(_outgoing, OpacityProperty, 1, 0, duration);
        _incoming.Opacity = 1;
        Animate(_incoming, OpacityProperty, 0, 1, duration);
        Animate(_incomingOffset, axis, distance, 0, duration,
            (_, _) => { if (revision == _revision) Stop(); });
    }

    private static void Animate(IAnimatable target, DependencyProperty property, double from, double to, int milliseconds, EventHandler? completed = null)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = MotionProfile.EaseOut()
        };
        if (completed is not null) animation.Completed += completed;
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void Stop()
    {
        _pending?.Abort();
        _pending = null;
        _revision++;
        _incoming.BeginAnimation(OpacityProperty, null);
        _outgoing.BeginAnimation(OpacityProperty, null);
        _incomingOffset.BeginAnimation(TranslateTransform.XProperty, null);
        _incomingOffset.BeginAnimation(TranslateTransform.YProperty, null);
        _outgoingOffset.BeginAnimation(TranslateTransform.XProperty, null);
        _outgoingOffset.BeginAnimation(TranslateTransform.YProperty, null);
        _incoming.Opacity = 1;
        _incomingOffset.X = _incomingOffset.Y = 0;
        _outgoingOffset.X = _outgoingOffset.Y = 0;
        _outgoing.Visibility = Visibility.Collapsed;
        _outgoing.Source = null;
        _previousIndex = Index;
    }
}
