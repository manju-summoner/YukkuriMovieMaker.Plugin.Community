using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Shippo
{
    internal class ShippoBrushSource(IGraphicsDevicesAndContext devices, ShippoBrushParameter parameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new NullReferenceException(nameof(brush));
        readonly DisposeCollector disposer = new();

        bool isFirst = true;
        System.Windows.Media.Color color, backgroundColor;
        double size, lineWidth;
        int bitmapWidth, bitmapHeight;
        Matrix3x2 matrix;

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        const float BezierArcK = 0.5522847498f;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var isChanged = false;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var color = parameter.Color;
            var backgroundColor = parameter.BackgroundColor;
            var zoom = parameter.Zoom.GetValue(frame, length, fps) / 100.0;
            var size = parameter.Size.GetValue(frame, length, fps) * zoom;
            var lineWidth = parameter.LineWidth.GetValue(frame, length, fps) * zoom;
            var matrix = parameter.CreateBrushMatrix(desc);

            var dc = devices.DeviceContext;
            var factory = devices.D2D.Factory;

            var r = (float)Math.Max(1.0, Math.Round(size));
            var scaledLineWidth = (float)Math.Max(0.1, lineWidth);

            var maximumBitmapSize = dc.MaximumBitmapSize;
            var bitmapScale = Math.Min(1.0, (double)maximumBitmapSize / (r * 2f));
            if (bitmapScale < 1.0)
            {
                r = (float)Math.Round(r * bitmapScale);
                scaledLineWidth = (float)(scaledLineWidth * bitmapScale);
                matrix *= Matrix3x2.CreateScale((float)(1.0 / bitmapScale));
            }

            var bitmapWidth = Math.Max(1, (int)Math.Round(r * 2f));
            var bitmapHeight = Math.Max(1, (int)Math.Round(r * 2f));

            if (isFirst
                || !this.color.Equals(color)
                || !this.backgroundColor.Equals(backgroundColor)
                || this.size != size
                || this.lineWidth != lineWidth
                || this.bitmapWidth != bitmapWidth
                || this.bitmapHeight != bitmapHeight)
            {
                using var strokeStyle = factory.CreateStrokeStyle(new StrokeStyleProperties
                {
                    StartCap = CapStyle.Round,
                    EndCap = CapStyle.Round,
                    LineJoin = LineJoin.Round,
                });

                if (bitmap != null)
                    disposer.RemoveAndDispose(ref bitmap);
                if (brush != null)
                    disposer.RemoveAndDispose(ref brush);

                bitmap = dc.CreateNotInitializedBitmap(bitmapWidth, bitmapHeight);
                dc.Target = bitmap;
                dc.BeginDraw();
                dc.Clear(null);

                using (var bgBrush = dc.CreateSolidColorBrush(ToColor4(backgroundColor)))
                using (var fgBrush = dc.CreateSolidColorBrush(ToColor4(color)))
                {
                    dc.FillRectangle(new Vortice.RawRectF(0, 0, bitmapWidth, bitmapHeight), bgBrush);
                    DrawShippoCell(dc, factory, r, scaledLineWidth, fgBrush, strokeStyle);
                }

                dc.EndDraw();
                dc.Target = null;

                disposer.Collect(bitmap);

                brush = dc.CreateBitmapBrush(
                    bitmap,
                    new BitmapBrushProperties1
                    {
                        ExtendModeX = ExtendMode.Wrap,
                        ExtendModeY = ExtendMode.Wrap,
                        InterpolationMode = InterpolationMode.MultiSampleLinear,
                    },
                    new BrushProperties(1f, matrix));

                disposer.Collect(brush);
                isChanged = true;
            }
            else if (!this.matrix.Equals(matrix))
            {
                if (brush != null)
                    disposer.RemoveAndDispose(ref brush);

                brush = dc.CreateBitmapBrush(
                    bitmap,
                    new BitmapBrushProperties1
                    {
                        ExtendModeX = ExtendMode.Wrap,
                        ExtendModeY = ExtendMode.Wrap,
                        InterpolationMode = InterpolationMode.MultiSampleLinear,
                    },
                    new BrushProperties(1f, matrix));

                disposer.Collect(brush);
                isChanged = true;
            }

            isFirst = false;
            this.color = color;
            this.backgroundColor = backgroundColor;
            this.size = size;
            this.lineWidth = lineWidth;
            this.bitmapWidth = bitmapWidth;
            this.bitmapHeight = bitmapHeight;
            this.matrix = matrix;

            return isChanged;
        }

        static void DrawShippoCell(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            var d = r * 2f;

            dc.DrawEllipse(new Ellipse(new Vector2(r, r), r, r), brush, lineWidth, strokeStyle);

            DrawQuarterArcBezier(dc, factory, new Vector2(0f, 0f), r, 0f, MathF.PI / 2f, lineWidth, brush, strokeStyle);
            DrawQuarterArcBezier(dc, factory, new Vector2(d, 0f), r, MathF.PI / 2f, MathF.PI, lineWidth, brush, strokeStyle);
            DrawQuarterArcBezier(dc, factory, new Vector2(d, d), r, MathF.PI, 3f * MathF.PI / 2f, lineWidth, brush, strokeStyle);
            DrawQuarterArcBezier(dc, factory, new Vector2(0f, d), r, 3f * MathF.PI / 2f, 2f * MathF.PI, lineWidth, brush, strokeStyle);
        }

        static void DrawQuarterArcBezier(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            Vector2 center,
            float r,
            float startAngle,
            float endAngle,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            var p0 = new Vector2(center.X + r * MathF.Cos(startAngle), center.Y + r * MathF.Sin(startAngle));
            var p3 = new Vector2(center.X + r * MathF.Cos(endAngle), center.Y + r * MathF.Sin(endAngle));

            var tx0 = new Vector2(-MathF.Sin(startAngle), MathF.Cos(startAngle));
            var tx3 = new Vector2(-MathF.Sin(endAngle), MathF.Cos(endAngle));

            var k = r * BezierArcK;
            var p1 = new Vector2(p0.X + k * tx0.X, p0.Y + k * tx0.Y);
            var p2 = new Vector2(p3.X - k * tx3.X, p3.Y - k * tx3.Y);

            using var geometry = factory.CreatePathGeometry();
            using (var sink = geometry.Open())
            {
                sink.BeginFigure(p0, FigureBegin.Hollow);
                sink.AddBezier(new BezierSegment { Point1 = p1, Point2 = p2, Point3 = p3 });
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
            }
            dc.DrawGeometry(geometry, brush, lineWidth, strokeStyle);
        }

        static Color4 ToColor4(System.Windows.Media.Color c)
            => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

        bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    disposer.Dispose();
                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
