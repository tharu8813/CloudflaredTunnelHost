using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CloudflaredTunnelHost.Infrastructure;

public static class Motion
{
    public static readonly DependencyProperty WatchProperty = DependencyProperty.RegisterAttached(
        "Watch", typeof(object), typeof(Motion), new PropertyMetadata(null, WatchChanged));
    public static readonly DependencyProperty RevealProperty = DependencyProperty.RegisterAttached(
        "Reveal", typeof(bool), typeof(Motion), new PropertyMetadata(false, Configure));
    public static readonly DependencyProperty StaggerProperty = DependencyProperty.RegisterAttached(
        "Stagger", typeof(bool), typeof(Motion), new PropertyMetadata(false));
    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached(
        "Interactive", typeof(bool), typeof(Motion), new PropertyMetadata(false, Configure));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(MotionState), typeof(Motion));

    public static object GetWatch(DependencyObject d) => d.GetValue(WatchProperty);
    public static void SetWatch(DependencyObject d, object value) => d.SetValue(WatchProperty, value);
    public static bool GetReveal(DependencyObject d) => (bool)d.GetValue(RevealProperty);
    public static void SetReveal(DependencyObject d, bool value) => d.SetValue(RevealProperty, value);
    public static bool GetStagger(DependencyObject d) => (bool)d.GetValue(StaggerProperty);
    public static void SetStagger(DependencyObject d, bool value) => d.SetValue(StaggerProperty, value);
    public static bool GetInteractive(DependencyObject d) => (bool)d.GetValue(InteractiveProperty);
    public static void SetInteractive(DependencyObject d, bool value) => d.SetValue(InteractiveProperty, value);

    private static void Configure(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element) State(element).QueueReveal();
    }

    private static void WatchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && e.NewValue is not false) State(element).QueueReveal();
    }

    private static MotionState State(FrameworkElement element)
    {
        if (element.GetValue(StateProperty) is not MotionState state)
        {
            state = new MotionState(element);
            element.SetValue(StateProperty, state);
        }
        return state;
    }

    private sealed class MotionState
    {
        private readonly FrameworkElement _element;
        private readonly ScaleTransform _scale = new();
        private readonly TranslateTransform _offset = new();
        private DispatcherOperation? _pending;
        private Popup? _popup;
        private bool _pressed;
        private bool _transformed;
        private bool _revealed;
        private readonly Dictionary<DependencyProperty, AnimationTimeline> _animations = new();

        public MotionState(FrameworkElement element)
        {
            _element = element;
            element.Loaded += (_, _) =>
            {
                if (_popup is null && element.Parent is Popup popup) { _popup = popup; popup.Closed += PopupClosed; }
                QueueReveal();
            };
            element.Unloaded += (_, _) =>
            {
                if (_popup is not null) { _popup.Closed -= PopupClosed; _popup = null; }
                Reset();
            };
            if (element is ContextMenu menu) menu.Closed += (_, _) => Reset();
            element.IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) QueueReveal(); else Reset(); };
            element.MouseEnter += (_, _) => Respond();
            element.MouseLeave += (_, _) => { _pressed = false; Respond(); };
            element.PreviewMouseLeftButtonDown += (_, _) => { _pressed = true; Respond(); };
            element.PreviewMouseLeftButtonUp += (_, _) => { _pressed = false; Respond(); };
            element.LostMouseCapture += (_, _) => { _pressed = false; Respond(); };
            element.IsEnabledChanged += (_, _) => { _pressed = false; Respond(); };
            element.PreviewKeyDown += (_, e) => RespondToKey(e, true);
            element.PreviewKeyUp += (_, e) => RespondToKey(e, false);
            element.LostKeyboardFocus += (_, _) => { _pressed = false; Respond(); };
        }

        private void PopupClosed(object? sender, EventArgs e) => Reset();

        private void RespondToKey(KeyEventArgs e, bool pressed)
        {
            if (_element is ButtonBase && e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Return) { _pressed = pressed; Respond(); }
        }

        private void Transform()
        {
            _scale.CenterX = _element.ActualWidth * (.5 - _element.RenderTransformOrigin.X);
            _scale.CenterY = _element.ActualHeight * (.5 - _element.RenderTransformOrigin.Y);
            if (_transformed) return;
            // Append our transforms so a caller's transform or binding stays intact.
            var group = new TransformGroup();
            group.Children.Add(_element.RenderTransform);
            group.Children.Add(_scale);
            group.Children.Add(_offset);
            _element.SetCurrentValue(UIElement.RenderTransformProperty, group);
            _transformed = true;
        }

        public void QueueReveal()
        {
            if (!_element.IsLoaded || !_element.IsVisible || _pending?.Status == DispatcherOperationStatus.Pending) return;
            _pending = _element.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (!_element.IsVisible || !_element.IsLoaded) return;
                if (GetReveal(_element) || GetWatch(_element) is not null and not false) Reveal();
            }));
        }

        private void Reveal()
        {
            if (!SystemParameters.ClientAreaAnimation) { Reset(); return; }
            // Bound data refreshes should not replay an entrance on an already-visible element.
            if (_revealed) return;
            if (GetStagger(_element) && _element is Panel panel)
            {
                _revealed = true;
                var index = 0;
                foreach (var child in panel.Children.OfType<FrameworkElement>().Where(child => child.IsVisible))
                    State(child).Enter(Math.Min(index++, MotionProfile.MaxStaggerSteps) * MotionProfile.StaggerMs);
            }
            else Enter(0);
        }

        private void Enter(int delay)
        {
            if (_revealed) return;
            _revealed = true;
            Transform();
            var opacity = (double)_element.GetAnimationBaseValue(UIElement.OpacityProperty);
            Animate(_element, UIElement.OpacityProperty, 0, opacity, MotionProfile.RevealFadeMs, delay);
            Animate(_offset, TranslateTransform.YProperty, MotionProfile.RevealTravel, 0, MotionProfile.RevealTravelMs, delay);
        }

        private void Respond()
        {
            if (!GetInteractive(_element) || !_element.IsLoaded || !_element.IsVisible) return;
            if (!SystemParameters.ClientAreaAnimation) { Reset(); return; }
            Transform();
            var active = _element.IsEnabled;
            // Hover tint lives in the template; hover scaling makes typography jitter.
            var scale = active && _pressed ? MotionProfile.PressScale : 1;
            var duration = _pressed ? MotionProfile.PressMs : MotionProfile.ReleaseMs;
            if (Math.Abs(_scale.ScaleX - scale) < .0001 && Math.Abs(_scale.ScaleY - scale) < .0001) return;
            Animate(_scale, ScaleTransform.ScaleXProperty, _scale.ScaleX, scale, duration, 0);
            Animate(_scale, ScaleTransform.ScaleYProperty, _scale.ScaleY, scale, duration, 0);
        }

        private void Animate(IAnimatable target, DependencyProperty property, double from, double to, int milliseconds, int delay)
        {
            if (target != _element) ((DependencyObject)target).SetValue(property, to);
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(milliseconds + delay)
            };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (delay > 0) animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds + delay)),
                MotionProfile.EaseOut()));
            animation.Completed += (_, _) =>
            {
                if (_animations.GetValueOrDefault(property) != animation) return;
                target.BeginAnimation(property, null);
                _animations.Remove(property);
            };
            _animations[property] = animation;
            target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void Reset()
        {
            _pending?.Abort();
            _animations.Clear();
            _pressed = false;
            _revealed = false;
            _element.BeginAnimation(UIElement.OpacityProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _offset.BeginAnimation(TranslateTransform.YProperty, null);
            _scale.ScaleX = _scale.ScaleY = 1;
            _offset.Y = 0;
            if (_element is Panel panel && GetStagger(_element))
                foreach (var child in panel.Children.OfType<FrameworkElement>())
                    if (child.GetValue(StateProperty) is MotionState state) state.Reset();
        }
    }
}
