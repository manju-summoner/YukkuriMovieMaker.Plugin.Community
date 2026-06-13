using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class DropIndicatorAdorner : Adorner
{
    private readonly double _x;
    private readonly double _width;
    private readonly Pen _pen;
    private readonly Pen _glowPen;

    private const double VerticalPadding = 2.0;
    private const double GlowWidthMultiplier = 3.0;

    public DropIndicatorAdorner(UIElement adornedElement, double x, double width, System.Windows.Media.Brush brush)
        : base(adornedElement)
    {
        _x = x;
        _width = width;
        IsHitTestVisible = false;

        _pen = new Pen(brush, width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        _pen.Freeze();

        var glowBrush = brush.Clone();
        glowBrush.Opacity = 0.25;
        glowBrush.Freeze();
        _glowPen = new Pen(glowBrush, width * GlowWidthMultiplier)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        _glowPen.Freeze();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var adornedRect = new Rect(AdornedElement.RenderSize);
        var top = adornedRect.Top + VerticalPadding;
        var bottom = adornedRect.Bottom - VerticalPadding;

        var snappedX = SnapToPixel(_x);

        drawingContext.DrawLine(_glowPen, new Point(snappedX, top), new Point(snappedX, bottom));
        drawingContext.DrawLine(_pen, new Point(snappedX, top), new Point(snappedX, bottom));

        DrawDiamondMarker(drawingContext, snappedX, top);
        DrawDiamondMarker(drawingContext, snappedX, bottom);
    }

    private void DrawDiamondMarker(DrawingContext ctx, double x, double y)
    {
        var size = _width * 2.0;
        var geometry = new StreamGeometry();
        using (var sgc = geometry.Open())
        {
            sgc.BeginFigure(new Point(x, y - size), true, true);
            sgc.LineTo(new Point(x + size, y), true, false);
            sgc.LineTo(new Point(x, y + size), true, false);
            sgc.LineTo(new Point(x - size, y), true, false);
        }
        geometry.Freeze();
        ctx.DrawGeometry(_pen.Brush, null, geometry);
    }

    private static double SnapToPixel(double value) => Math.Round(value - 0.5) + 0.5;
}
