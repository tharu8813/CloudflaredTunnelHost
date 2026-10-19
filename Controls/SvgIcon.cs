using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace CloudflaredTunnelHost.Controls;

// The embedded SVG is the source of truth; WPF caches its parsed paths at device-independent resolution.
public sealed class SvgIcon : FrameworkElement
{
    private static readonly Dictionary<string, Geometry> Cache = new();
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(SvgIcon), new FrameworkPropertyMetadata("network", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(SvgIcon), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 20 : Math.Min(20, availableSize.Width), double.IsInfinity(availableSize.Height) ? 20 : Math.Min(20, availableSize.Height));
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        context.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        context.DrawGeometry(null, new Pen(Stroke, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, LoadGeometry(Kind));
        context.Pop();
    }
    public static Geometry LoadGeometry(string kind)
    {
        if (Cache.TryGetValue(kind, out var geometry)) return geometry;
        var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/CloudflaredTunnelHost;component/Assets/Icons/{kind}.svg")) ?? throw new IOException("SVG 아이콘이 없습니다: " + kind);
        using var stream = resource.Stream;
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var svg = XDocument.Load(reader);
        var group = new GeometryGroup();
        foreach (var path in svg.Descendants().Where(x => x.Name.LocalName == "path")) group.Children.Add(Geometry.Parse(path.Attribute("d")!.Value));
        group.Freeze(); Cache[kind] = group; return group;
    }
}
