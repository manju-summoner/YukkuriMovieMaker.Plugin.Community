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
        const float CenterDotRadiusRatio = 0.08f;
        const float CornerDotRadiusRatio = 0.06f;
        const float DoubleCircleInnerRatio = 0.4142135624f;
        const float HanabishiPetalLengthRatio = 0.55f;
        const float HanabishiPetalWidthRatio = 0.22f;

        readonly DisposeCollector disposer = new();

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        bool isFirst = true;
        ShippoPattern pattern;
        System.Windows.Media.Color color, backgroundColor;
        double size, lineWidth;
        int bitmapWidth, bitmapHeight;
        Matrix3x2 matrix;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var newPattern = parameter.Pattern;
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
                || this.pattern != newPattern
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
                    DrawShippoCell(dc, factory, newPattern, r, scaledLineWidth, fgBrush, strokeStyle);

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
            this.pattern = newPattern;
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
            ShippoPattern pattern,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            switch (pattern)
            {
                case ShippoPattern.Basic:
                    DrawBasicCell(dc, factory, r, lineWidth, brush, strokeStyle);
                    break;
                case ShippoPattern.DoubleCircle:
                    DrawDoubleCircleCell(dc, factory, r, lineWidth, brush, strokeStyle);
                    break;
                case ShippoPattern.CenterDot:
                    DrawCenterDotCell(dc, factory, r, lineWidth, brush, strokeStyle);
                    break;
                case ShippoPattern.CornerDot:
                    DrawCornerDotCell(dc, factory, r, lineWidth, brush, strokeStyle);
                    break;
                case ShippoPattern.Hanabishi:
                    DrawHanabishiCell(dc, factory, r, lineWidth, brush, strokeStyle);
                    break;
                default:
                    DrawBasicCell(dc, factory, r, lineWidth, brush, strokeStyle);
                    break;
            }
        }

        static void DrawBasicCell(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            DrawCenterCircleOutline(dc, r, lineWidth, brush, strokeStyle);
            DrawCornerQuarterArcs(dc, factory, r, r, lineWidth, brush, strokeStyle);
        }

        static void DrawDoubleCircleCell(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            DrawCenterCircleOutline(dc, r, lineWidth, brush, strokeStyle);
            DrawCornerQuarterArcs(dc, factory, r, r, lineWidth, brush, strokeStyle);

            var innerR = r * DoubleCircleInnerRatio;
            dc.DrawEllipse(new Ellipse(new Vector2(r, r), innerR, innerR), brush, lineWidth, strokeStyle);
            DrawCornerQuarterArcs(dc, factory, r, innerR, lineWidth, brush, strokeStyle);
        }

        static void DrawCenterDotCell(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            DrawCenterCircleOutline(dc, r, lineWidth, brush, strokeStyle);
            DrawCornerQuarterArcs(dc, factory, r, r, lineWidth, brush, strokeStyle);

            var dotR = r * CenterDotRadiusRatio;
            dc.FillEllipse(new Ellipse(new Vector2(r, r), dotR, dotR), brush);
        }

        static void DrawCornerDotCell(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            DrawCenterCircleOutline(dc, r, lineWidth, brush, strokeStyle);
            DrawCornerQuarterArcs(dc, factory, r, r, lineWidth, brush, strokeStyle);

            var dotR = r * CornerDotRadiusRatio;
            var d = r * 2f;
            dc.FillEllipse(new Ellipse(new Vector2(r, 0f), dotR, dotR), brush);
            dc.FillEllipse(new Ellipse(new Vector2(d, r), dotR, dotR), brush);
            dc.FillEllipse(new Ellipse(new Vector2(r, d), dotR, dotR), brush);
            dc.FillEllipse(new Ellipse(new Vector2(0f, r), dotR, dotR), brush);
        }

        static void DrawHanabishiCell(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            DrawCenterCircleOutline(dc, r, lineWidth, brush, strokeStyle);
            DrawCornerQuarterArcs(dc, factory, r, r, lineWidth, brush, strokeStyle);

            var petalLength = r * HanabishiPetalLengthRatio;
            var petalWidth = r * HanabishiPetalWidthRatio;
            DrawHanabishi(dc, factory, new Vector2(r, r), petalLength, petalWidth, lineWidth, brush, strokeStyle);
        }

        static void DrawCenterCircleOutline(
            ID2D1DeviceContext dc,
            float r,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            dc.DrawEllipse(new Ellipse(new Vector2(r, r), r, r), brush, lineWidth, strokeStyle);
        }

        static void DrawCornerQuarterArcs(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float r,
            float arcRadius,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            var d = r * 2.0f;

            DrawQuarterArcBezier(dc, factory, new Vector2(0f, 0f), arcRadius, 0f, MathF.PI / 2f, lineWidth, brush, strokeStyle);
            DrawQuarterArcBezier(dc, factory, new Vector2(d, 0f), arcRadius, MathF.PI / 2f, MathF.PI, lineWidth, brush, strokeStyle);
            DrawQuarterArcBezier(dc, factory, new Vector2(d, d), arcRadius, MathF.PI, 3f * MathF.PI / 2f, lineWidth, brush, strokeStyle);
            DrawQuarterArcBezier(dc, factory, new Vector2(0f, d), arcRadius, 3f * MathF.PI / 2f, 2f * MathF.PI, lineWidth, brush, strokeStyle);
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

        static void DrawHanabishi(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            Vector2 center,
            float petalLength,
            float petalWidth,
            float lineWidth,
            ID2D1SolidColorBrush brush,
            ID2D1StrokeStyle strokeStyle)
        {
            var sagitta = petalWidth / 2f;

            for (var i = 0; i < 4; i++)
            {
                var angle = i * MathF.PI / 2f;
                var cos = MathF.Cos(angle);
                var sin = MathF.Sin(angle);

                var tipInner = Rotate(new Vector2(0f, 0f), cos, sin) + center;
                var tipOuter = Rotate(new Vector2(petalLength, 0f), cos, sin) + center;

                using var geometry = factory.CreatePathGeometry();
                using (var sink = geometry.Open())
                {
                    sink.BeginFigure(tipInner, FigureBegin.Hollow);
                    AppendArcBezier(sink, tipInner, tipOuter, sagitta);
                    AppendArcBezier(sink, tipOuter, tipInner, sagitta);
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();
                }
                dc.DrawGeometry(geometry, brush, lineWidth, strokeStyle);
            }
        }

        static void AppendArcBezier(
            ID2D1GeometrySink sink,
            Vector2 start,
            Vector2 end,
            float sagitta)
        {
            var chord = end - start;
            var chordLength = chord.Length();
            if (chordLength < 1e-6f || sagitta <= 0f)
            {
                sink.AddBezier(new BezierSegment { Point1 = start, Point2 = end, Point3 = end });
                return;
            }

            var chordDir = chord / chordLength;
            var halfChord = chordLength / 2f;
            var radius = (halfChord * halfChord + sagitta * sagitta) / (2f * sagitta);
            var halfAngle = MathF.Atan2(halfChord, radius - sagitta);
            var k = 4f / 3f * MathF.Tan(halfAngle / 2f);

            var tangentStart = RotateVector(chordDir, halfAngle);
            var tangentEnd = RotateVector(chordDir, -halfAngle);

            var p1 = start + tangentStart * (k * radius);
            var p2 = end - tangentEnd * (k * radius);

            sink.AddBezier(new BezierSegment { Point1 = p1, Point2 = p2, Point3 = end });
        }

        static Vector2 RotateVector(Vector2 v, float angle)
        {
            var c = MathF.Cos(angle);
            var s = MathF.Sin(angle);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        static Vector2 Rotate(Vector2 v, float cos, float sin)
            => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

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
