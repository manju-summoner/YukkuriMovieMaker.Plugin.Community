using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View.Adorners;

public class RectSelectionAdorner : SelectionAdornerBase
{
    private Point _end;
    private Point _start;
    private bool _visible;

    public RectSelectionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    public Rect CurrentRect => new(_start, _end);

    public void Update(Point start, Point end)
    {
        _start = start;
        _end = end;
        _visible = true;
        InvalidateVisual();
    }

    public override void Clear()
    {
        _visible = false;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_visible) return;

        var rect = new Rect(_start, _end);

        dc.DrawRectangle(
            new SolidColorBrush(SystemColors.HighlightColor with { A = 40 }),
            new Pen(SystemColors.HighlightBrush, 0.5),
            rect);
    }
}