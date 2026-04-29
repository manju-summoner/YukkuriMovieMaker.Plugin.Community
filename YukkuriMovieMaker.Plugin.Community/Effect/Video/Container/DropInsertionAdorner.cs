using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class DropInsertionAdorner : Adorner
{
    private readonly Pen _pen;

    public DropInsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _pen = new Pen(SystemColors.HighlightBrush, 2.0);
        _pen.Freeze();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;
        drawingContext.DrawLine(_pen, new Point(0, h), new Point(w, h));
    }
}