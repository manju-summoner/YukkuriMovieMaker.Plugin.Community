using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View.Adorners;

public class LassoSelectionAdorner : SelectionAdornerBase
{
    private readonly List<Point> _points = new();
    private bool _visible;

    public LassoSelectionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public IReadOnlyList<Point> Points => _points;

    public void Begin(Point start)
    {
        _points.Clear();
        _points.Add(start);
        _visible = true;
        InvalidateVisual();
    }

    public void AddPoint(Point p)
    {
        if (!_visible) return;
        _points.Add(p);
        InvalidateVisual();
    }

    public override void Clear()
    {
        _visible = false;
        _points.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_visible || _points.Count < 2) return;

        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_points[0], true, true);
            ctx.PolyLineTo(_points.Skip(1).ToList(), true, true);
        }

        geometry.Freeze();

        dc.DrawGeometry(
            new SolidColorBrush(SystemColors.HighlightColor with { A = 40 }),
            new Pen(SystemColors.HighlightBrush, 0.5),
            geometry);
    }
}