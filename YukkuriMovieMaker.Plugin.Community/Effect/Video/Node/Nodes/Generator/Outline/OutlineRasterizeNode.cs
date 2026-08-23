using System.ComponentModel.DataAnnotations;
using System.Numerics;
using SkiaSharp;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using AlphaMode = Vortice.DCommon.AlphaMode;
using BezierSegment = Vortice.Direct2D1.BezierSegment;
using Colors = System.Windows.Media.Colors;
using D2DColor = Vortice.Mathematics.Color;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;
using QuadraticBezierSegment = Vortice.Direct2D1.QuadraticBezierSegment;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

public enum FillRuleKind
{
    [Display(Name = nameof(TextNode.FillRuleNonZero), ResourceType = typeof(TextNode))]
    NonZero,

    [Display(Name = nameof(TextNode.FillRuleEvenOdd), ResourceType = typeof(TextNode))]
    EvenOdd
}

[Node(typeof(OutlineCategory), nameof(TextNode.OutlineRasterizeNode), nameof(TextNode.OutlineRasterizeNodeDescription),
    typeof(TextNode))]
public class OutlineRasterizeNode : NodeLogic
{
    private const float Padding = 2f;

    private ID2D1Bitmap1? _bitmap;
    private int _bitmapHeight = -1;
    private int _bitmapWidth = -1;

    [InputPort(nameof(TextNode.OutlinePortLabel), nameof(TextNode.OutlineRasterizeInputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.MediumPurple))]
    public OutlineWrapper? Outline
    {
        get => GetInput<OutlineWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StrokeWidthLabel), nameof(TextNode.StrokeWidthDescription), typeof(TextNode))]
    [NumberPortControl(Min = 0, Max = 2000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float StrokeWidth
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StrokeOffsetXLabel), nameof(TextNode.StrokeOffsetXDescription), typeof(TextNode))]
    [NumberPortControl(Min = -4000, Max = 4000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float StrokeOffsetX
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StrokeOffsetYLabel), nameof(TextNode.StrokeOffsetYDescription), typeof(TextNode))]
    [NumberPortControl(Min = -4000, Max = 4000, Default = 0)]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public float StrokeOffsetY
    {
        get => GetInput<float>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.StrokeBrushLabel), nameof(TextNode.StrokeBrushDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.LawnGreen))]
    public BrushWrapper? StrokeBrush
    {
        get => GetInput<BrushWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.FillBrushLabel), nameof(TextNode.FillBrushDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.LawnGreen))]
    public BrushWrapper? FillBrush
    {
        get => GetInput<BrushWrapper>();
        set => SetInput(value);
    }

    [InputPort(nameof(TextNode.SelfIntersectRuleLabel), nameof(TextNode.SelfIntersectRuleDescription),
        typeof(TextNode))]
    [EnumPortControl(Default = 0, IsEditable = false, Items = typeof(FillRuleKind))]
    [PortColorSetting(nameof(Colors.DarkOrange))]
    public int FillRule
    {
        get => GetInput<int>();
        set => SetInput(value);
    }

    [OutputPort(nameof(TextNode.OutputImagePortLabel), nameof(TextNode.RasterizeOutputDescription), typeof(TextNode))]
    [PortColorSetting(nameof(Colors.CornflowerBlue))]
    public ImageWrapper? Output
    {
        get => GetOutput<ImageWrapper>();
        set => SetOutput(value);
    }

    protected override Task Calculate()
    {
        if (EvaluationContext is null)
            return Task.FromException(new NullReferenceException(nameof(EvaluationContext)));
        if (Outline?.Path is null)
            return Task.FromException(new NullReferenceException(nameof(Outline)));

        var hasFill = FillBrush?.Brush is not null && FillBrush.Brush.NativePointer != nint.Zero;
        var hasStroke = StrokeBrush?.Brush is not null && StrokeBrush.Brush.NativePointer != nint.Zero &&
                        StrokeWidth > 0f;
        if (!hasFill && !hasStroke)
            return Task.FromException(new NullReferenceException(nameof(FillBrush)));

        var deviceContext = EvaluationContext.Devices.DeviceContext;
        var factory = deviceContext.Factory;

        using var pathGeometry = factory.CreatePathGeometry();
        using (var sink = pathGeometry.Open())
        {
            sink.SetFillMode((FillRuleKind)FillRule == FillRuleKind.NonZero ? FillMode.Winding : FillMode.Alternate);
            BuildGeometry(sink, Outline.Path);
            sink.Close();
        }

        var bounds = Outline.Path.Bounds;
        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right;
        var bottom = bounds.Bottom;

        if (hasStroke)
        {
            var halfStroke = StrokeWidth / 2f;
            left = System.Math.Min(left, bounds.Left - halfStroke + StrokeOffsetX);
            top = System.Math.Min(top, bounds.Top - halfStroke + StrokeOffsetY);
            right = System.Math.Max(right, bounds.Right + halfStroke + StrokeOffsetX);
            bottom = System.Math.Max(bottom, bounds.Bottom + halfStroke + StrokeOffsetY);
        }

        left = MathF.Min(left, 0f) - Padding;
        top = MathF.Min(top, 0f) - Padding;
        right = MathF.Max(right, 0f) + Padding;
        bottom = MathF.Max(bottom, 0f) + Padding;

        var width = System.Math.Max(1, (int)MathF.Ceiling(right - left));
        var height = System.Math.Max(1, (int)MathF.Ceiling(bottom - top));
        var translateX = -left;
        var translateY = -top;

        if (_bitmap is null || _bitmapWidth != width || _bitmapHeight != height)
        {
            _bitmap?.Dispose();
            var bitmapProperties = new BitmapProperties1(
                new D2DPixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96, 96, BitmapOptions.Target);
            _bitmap = deviceContext.CreateBitmap(new SizeI(width, height), IntPtr.Zero, 0, bitmapProperties);
            _bitmapWidth = width;
            _bitmapHeight = height;
        }

        var previousTarget = deviceContext.Target;
        deviceContext.Target = _bitmap;
        deviceContext.BeginDraw();
        deviceContext.Clear(new D2DColor(0, 0, 0, 0));

        if (hasFill)
        {
            deviceContext.Transform = Matrix3x2.CreateTranslation(translateX, translateY);
            deviceContext.FillGeometry(pathGeometry, FillBrush!.Brush!);
        }

        if (hasStroke)
        {
            deviceContext.Transform =
                Matrix3x2.CreateTranslation(translateX + StrokeOffsetX, translateY + StrokeOffsetY);
            deviceContext.DrawGeometry(pathGeometry, StrokeBrush!.Brush!, StrokeWidth);
        }

        deviceContext.EndDraw();
        deviceContext.Transform = Matrix3x2.Identity;
        deviceContext.Target = previousTarget;

        Output = new ImageWrapper { Image = _bitmap };
        return Task.CompletedTask;
    }

    private static void BuildGeometry(ID2D1GeometrySink sink, SKPath path)
    {
        var figureOpen = false;
        var points = new SKPoint[4];
        using var iterator = path.CreateIterator(false);
        SKPathVerb verb;

        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
            switch (verb)
            {
                case SKPathVerb.Move:
                    if (figureOpen) sink.EndFigure(FigureEnd.Open);
                    sink.BeginFigure(new Vector2(points[0].X, points[0].Y), FigureBegin.Filled);
                    figureOpen = true;
                    break;
                case SKPathVerb.Line:
                    sink.AddLine(new Vector2(points[1].X, points[1].Y));
                    break;
                case SKPathVerb.Quad:
                    sink.AddQuadraticBezier(new QuadraticBezierSegment
                    {
                        Point1 = new Vector2(points[1].X, points[1].Y),
                        Point2 = new Vector2(points[2].X, points[2].Y)
                    });
                    break;
                case SKPathVerb.Conic:
                    AddConicAsLines(sink, points[0], points[1], points[2], iterator.ConicWeight());
                    break;
                case SKPathVerb.Cubic:
                    sink.AddBezier(new BezierSegment
                    {
                        Point1 = new Vector2(points[1].X, points[1].Y),
                        Point2 = new Vector2(points[2].X, points[2].Y),
                        Point3 = new Vector2(points[3].X, points[3].Y)
                    });
                    break;
                case SKPathVerb.Close:
                    sink.EndFigure(FigureEnd.Closed);
                    figureOpen = false;
                    break;
            }

        if (figureOpen) sink.EndFigure(FigureEnd.Open);
    }

    private static void AddConicAsLines(ID2D1GeometrySink sink, SKPoint p0, SKPoint p1, SKPoint p2, float w)
    {
        const int segments = 8;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var oneMinusT = 1f - t;
            // 有理2次ベジェ（円錐曲線）の重み付き評価。分母の重みが分子側にも掛かる点が通常の2次ベジェと異なる。
            var wb = oneMinusT * oneMinusT + 2f * t * oneMinusT * w + t * t;
            var x = (oneMinusT * oneMinusT * p0.X + 2f * t * oneMinusT * w * p1.X + t * t * p2.X) / wb;
            var y = (oneMinusT * oneMinusT * p0.Y + 2f * t * oneMinusT * w * p1.Y + t * t * p2.Y) / wb;
            sink.AddLine(new Vector2(x, y));
        }
    }

    public override void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _bitmapWidth = -1;
        _bitmapHeight = -1;
        base.Dispose();
    }
}