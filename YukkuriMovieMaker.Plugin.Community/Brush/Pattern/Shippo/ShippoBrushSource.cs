using System;
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
        public ID2D1Brush Brush => brush ?? throw new InvalidOperationException(
            $"{nameof(Update)} must be called before accessing {nameof(Brush)}.");

        const float BezierArcK = 0.5522847498f;

        readonly DisposeCollector disposer = new();

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        bool isFirst = true;
        System.Windows.Media.Color color, backgroundColor;
        double size, lineWidth;
        int bitmapWidth, bitmapHeight;
        Matrix3x2 matrix;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var newColor = parameter.Color;
            var newBackgroundColor = parameter.BackgroundColor;
            var zoom = parameter.Zoom.GetValue(frame, length, fps) / 100.0;
            var newSize = parameter.Size.GetValue(frame, length, fps) * zoom;
            var newLineWidth = parameter.LineWidth.GetValue(frame, length, fps) * zoom;
            var newMatrix = parameter.CreateBrushMatrix(desc);

            var dc = devices.DeviceContext;
            var factory = devices.D2D.Factory;

            var r = (float)Math.Max(1.0, Math.Round(newSize));
            var scaledLineWidth = (float)Math.Max(0.1, newLineWidth);

            var maximumBitmapSize = dc.MaximumBitmapSize;
            var bitmapScale = Math.Min(1.0, (double)maximumBitmapSize / (r * 2.0));
            if (bitmapScale < 1.0)
            {
                r = (float)Math.Round(r * bitmapScale);
                scaledLineWidth = (float)(scaledLineWidth * bitmapScale);
                newMatrix *= Matrix3x2.CreateScale((float)(1.0 / bitmapScale));
            }

            var newBitmapWidth = Math.Max(1, (int)Math.Round(r * 2.0));
            var newBitmapHeight = Math.Max(1, (int)Math.Round(r * 2.0));

            var bitmapNeedsUpdate = isFirst
                || !this.color.Equals(newColor)
                || !this.backgroundColor.Equals(newBackgroundColor)
                || this.size != newSize
                || this.lineWidth != newLineWidth
                || this.bitmapWidth != newBitmapWidth
                || this.bitmapHeight != newBitmapHeight;

            var isChanged = false;

            if (bitmapNeedsUpdate)
            {
                using var strokeStyle = factory.CreateStrokeStyle(new StrokeStyleProperties
                {
                    StartCap = CapStyle.Round,
                    EndCap = CapStyle.Round,
                    LineJoin = LineJoin.Round,
                });

                if (bitmap != null) disposer.RemoveAndDispose(ref bitmap);
                if (brush != null) disposer.RemoveAndDispose(ref brush);

                bitmap = dc.CreateNotInitializedBitmap(newBitmapWidth, newBitmapHeight);
                dc.Target = bitmap;
                try
                {
                    dc.BeginDraw();
                    dc.Clear(null);

                    using var bgBrush = dc.CreateSolidColorBrush(ToColor4(newBackgroundColor));
                    using var fgBrush = dc.CreateSolidColorBrush(ToColor4(newColor));

                    dc.FillRectangle(new Vortice.RawRectF(0, 0, newBitmapWidth, newBitmapHeight), bgBrush);
                    DrawShippoCell(dc, factory, r, scaledLineWidth, fgBrush, strokeStyle);

                    dc.EndDraw();
                }
                finally
                {
                    dc.Target = null;
                }

                disposer.Collect(bitmap);
                isChanged = true;
            }

            if (bitmapNeedsUpdate || !this.matrix.Equals(newMatrix))
            {
                if (brush != null) disposer.RemoveAndDispose(ref brush);

                brush = dc.CreateBitmapBrush(
                    bitmap,
                    new BitmapBrushProperties1
                    {
                        ExtendModeX = ExtendMode.Wrap,
                        ExtendModeY = ExtendMode.Wrap,
                        InterpolationMode = InterpolationMode.MultiSampleLinear,
                    },
                    new BrushProperties(1f, newMatrix));

                disposer.Collect(brush);
                isChanged = true;
            }

            isFirst = false;
            this.color = newColor;
            this.backgroundColor = newBackgroundColor;
            this.size = newSize;
            this.lineWidth = newLineWidth;
            this.bitmapWidth = newBitmapWidth;
            this.bitmapHeight = newBitmapHeight;
            this.matrix = newMatrix;

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
            var d = r * 2.0f;

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

        #region IDisposable Support
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // マネージド状態を破棄します (マネージド オブジェクト)
                    disposer.Dispose();
                }

                // アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~ShippoBrushSource()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
