using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public class RectSelectionAdorner : Adorner
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

    public void Clear()
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