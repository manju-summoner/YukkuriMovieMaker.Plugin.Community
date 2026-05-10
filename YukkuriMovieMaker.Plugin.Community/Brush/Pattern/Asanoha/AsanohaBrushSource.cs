using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Asanoha
{
    internal class AsanohaBrushSource(IGraphicsDevicesAndContext devices, AsanohaBrushParameter parameter) : IBrushSource
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

            var w = (float)Math.Round(r * MathF.Sqrt(3f));
            var h = (float)Math.Round(r * 3f);

            var maximumBitmapSize = dc.MaximumBitmapSize;
            var bitmapScale = Math.Min(1.0, (double)maximumBitmapSize / Math.Max(w, h));
            if (bitmapScale < 1.0)
            {
                r = (float)Math.Round(r * bitmapScale);
                scaledLineWidth = (float)(scaledLineWidth * bitmapScale);
                w = (float)Math.Round(r * MathF.Sqrt(3f));
                h = (float)Math.Round(r * 3f);
                matrix *= Matrix3x2.CreateScale((float)(1.0 / bitmapScale));
            }

            var bitmapWidth = Math.Max(1, (int)w);
            var bitmapHeight = Math.Max(1, (int)h);

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

                    DrawAsanohaCell(dc, w / 2f, r, r, scaledLineWidth, fgBrush, strokeStyle);
                    DrawAsanohaCell(dc, -w / 2f, r, r, scaledLineWidth, fgBrush, strokeStyle);
                    DrawAsanohaCell(dc, 3f * w / 2f, r, r, scaledLineWidth, fgBrush, strokeStyle);
                    DrawAsanohaCell(dc, 0f, 5f * r / 2f, r, scaledLineWidth, fgBrush, strokeStyle);
                    DrawAsanohaCell(dc, w, 5f * r / 2f, r, scaledLineWidth, fgBrush, strokeStyle);
                    DrawAsanohaCell(dc, 0f, -r / 2f, r, scaledLineWidth, fgBrush, strokeStyle);
                    DrawAsanohaCell(dc, w, -r / 2f, r, scaledLineWidth, fgBrush, strokeStyle);
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

        static void DrawAsanohaCell(
            ID2D1DeviceContext dc,
            float cx, float cy, float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            var vertices = GetHexVertices(cx, cy, r);

            for (int i = 0; i < 6; i++)
                dc.DrawLine(new Vector2(cx, cy), vertices[i], brush, lineWidth, strokeStyle);

            for (int i = 0; i < 6; i++)
            {
                var v0 = vertices[i];
                var v1 = vertices[(i + 1) % 6];
                var mid = new Vector2((v0.X + v1.X) / 2f, (v0.Y + v1.Y) / 2f);
                dc.DrawLine(new Vector2(cx, cy), mid, brush, lineWidth, strokeStyle);
                dc.DrawLine(v0, mid, brush, lineWidth, strokeStyle);
                dc.DrawLine(v1, mid, brush, lineWidth, strokeStyle);
            }

            for (int i = 0; i < 6; i++)
                dc.DrawLine(vertices[i], vertices[(i + 1) % 6], brush, lineWidth, strokeStyle);
        }

        static Vector2[] GetHexVertices(float cx, float cy, float r)
        {
            var vertices = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                var angle = MathF.PI / 6f + i * MathF.PI / 3f;
                vertices[i] = new Vector2(cx + r * MathF.Cos(angle), cy + r * MathF.Sin(angle));
            }
            return vertices;
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
