using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Seigaiha
{
    internal class SeigaihaBrushSource(IGraphicsDevicesAndContext devices, SeigaihaBrushParameter parameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new InvalidOperationException(
            $"{nameof(Update)} must be called before accessing {nameof(Brush)}.");

        readonly DisposeCollector disposer = new();

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        bool isFirst = true;
        bool gradientEnabled;
        System.Windows.Media.Color color, outerColor, innerColor, backgroundColor, strokeColor;
        double rawRadius, rawLineWidth, rawZoom, ringCount;
        int bitmapWidth, bitmapHeight;
        Matrix3x2 matrix;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var newGradientEnabled = parameter.GradientEnabled;
            var newColor = parameter.Color;
            var newOuterColor = parameter.OuterColor;
            var newInnerColor = parameter.InnerColor;
            var newBackgroundColor = parameter.BackgroundColor;
            var newStrokeColor = parameter.StrokeColor;
            var newRawZoom = parameter.Zoom.GetValue(frame, length, fps);
            var newRawRadius = parameter.Radius.GetValue(frame, length, fps);
            var newRawLineWidth = parameter.LineWidth.GetValue(frame, length, fps);
            var newRingCount = Math.Max(1, Math.Round(parameter.RingCount.GetValue(frame, length, fps)));
            var newMatrix = parameter.CreateBrushMatrix(desc);

            var dc = devices.DeviceContext;
            var factory = devices.D2D.Factory;

            var zoom = newRawZoom / 100.0;
            var radius = newRawRadius * zoom;
            var lineWidth = newRawLineWidth * zoom;

            var r = (float)Math.Max(1.0, Math.Round(radius));
            var scaledLineWidth = (float)Math.Max(0.0, lineWidth);

            var maximumBitmapSize = dc.MaximumBitmapSize;
            var bitmapScale = Math.Min(1.0, (double)maximumBitmapSize / (r * 2.0));
            if (bitmapScale < 1.0)
            {
                r = (float)Math.Round(r * bitmapScale);
                scaledLineWidth = (float)(scaledLineWidth * bitmapScale);
                newMatrix *= Matrix3x2.CreateScale((float)(1.0 / bitmapScale));
            }

            var newBitmapWidth = Math.Min(maximumBitmapSize, Math.Max(1, (int)(r * 2.0)));
            var newBitmapHeight = Math.Min(maximumBitmapSize, Math.Max(1, (int)r));

            var bitmapNeedsUpdate = isFirst
                || this.gradientEnabled != newGradientEnabled
                || !this.color.Equals(newColor)
                || !this.outerColor.Equals(newOuterColor)
                || !this.innerColor.Equals(newInnerColor)
                || !this.backgroundColor.Equals(newBackgroundColor)
                || !this.strokeColor.Equals(newStrokeColor)
                || this.rawRadius != newRawRadius
                || this.rawLineWidth != newRawLineWidth
                || this.rawZoom != newRawZoom
                || this.ringCount != newRingCount
                || this.bitmapWidth != newBitmapWidth
                || this.bitmapHeight != newBitmapHeight;

            var isChanged = false;

            if (bitmapNeedsUpdate)
            {
                var rings = (int)newRingCount;

                if (bitmap != null) disposer.RemoveAndDispose(ref bitmap);
                if (brush != null) disposer.RemoveAndDispose(ref brush);

                bitmap = dc.CreateNotInitializedBitmap(newBitmapWidth, newBitmapHeight);
                dc.Target = bitmap;
                try
                {
                    dc.BeginDraw();
                    dc.Clear(null);

                    using var bgBrush = dc.CreateSolidColorBrush(ToColor4(newBackgroundColor));
                    using var strokeBrush = dc.CreateSolidColorBrush(ToColor4(newStrokeColor));

                    dc.FillRectangle(new Vortice.RawRectF(0, 0, newBitmapWidth, newBitmapHeight), bgBrush);

                    for (int j = -2; j <= 4; j++)
                    {
                        float cy = j * (r / 2f);
                        bool isOdd = (j % 2 != 0);
                        int startK = isOdd ? -1 : 0;
                        int endK = isOdd ? 3 : 2;

                        for (int k = startK; k <= endK; k += 2)
                        {
                            float cx = k * r;
                            if (newGradientEnabled)
                                DrawWaveGradient(dc, factory, cx, cy, r, rings, newOuterColor, newInnerColor, strokeBrush, scaledLineWidth);
                            else
                                DrawWaveFlat(dc, factory, cx, cy, r, rings, ToColor4(newColor), strokeBrush, scaledLineWidth);
                        }
                    }

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
            this.gradientEnabled = newGradientEnabled;
            this.color = newColor;
            this.outerColor = newOuterColor;
            this.innerColor = newInnerColor;
            this.backgroundColor = newBackgroundColor;
            this.strokeColor = newStrokeColor;
            this.rawRadius = newRawRadius;
            this.rawLineWidth = newRawLineWidth;
            this.rawZoom = newRawZoom;
            this.ringCount = newRingCount;
            this.bitmapWidth = newBitmapWidth;
            this.bitmapHeight = newBitmapHeight;
            this.matrix = newMatrix;

            return isChanged;
        }

        static void DrawWaveFlat(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float cx,
            float cy,
            float r,
            int rings,
            Color4 fillColor,
            ID2D1SolidColorBrush strokeBrush,
            float strokeWidth)
        {
            using var baseCircle = factory.CreateEllipseGeometry(new Ellipse(new Vector2(cx, cy), r, r));
            using var fillBrush = dc.CreateSolidColorBrush(fillColor);
            dc.FillGeometry(baseCircle, fillBrush);

            if (strokeWidth > 0f)
            {
                for (int i = 1; i <= rings; i++)
                {
                    var ri = r * i / rings;
                    using var circle = factory.CreateEllipseGeometry(new Ellipse(new Vector2(cx, cy), ri, ri));
                    dc.DrawGeometry(circle, strokeBrush, strokeWidth);
                }
            }
        }

        static void DrawWaveGradient(
            ID2D1DeviceContext dc,
            ID2D1Factory factory,
            float cx,
            float cy,
            float r,
            int rings,
            System.Windows.Media.Color outerColor,
            System.Windows.Media.Color innerColor,
            ID2D1SolidColorBrush strokeBrush,
            float strokeWidth)
        {
            for (int i = rings; i >= 1; i--)
            {
                var t = rings > 1 ? (float)(i - 1) / (rings - 1) : 1f;
                var fillColor = LerpColor(innerColor, outerColor, t);
                var ri = r * i / rings;

                using var circle = factory.CreateEllipseGeometry(new Ellipse(new Vector2(cx, cy), ri, ri));
                using var fillBrush = dc.CreateSolidColorBrush(ToColor4(fillColor));
                dc.FillGeometry(circle, fillBrush);
            }

            if (strokeWidth > 0f)
            {
                for (int i = 1; i <= rings; i++)
                {
                    var ri = r * i / rings;
                    using var circle = factory.CreateEllipseGeometry(new Ellipse(new Vector2(cx, cy), ri, ri));
                    dc.DrawGeometry(circle, strokeBrush, strokeWidth);
                }
            }
        }

        static System.Windows.Media.Color LerpColor(System.Windows.Media.Color a, System.Windows.Media.Color b, float t)
        {
            return System.Windows.Media.Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
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
        // ~SeigaihaBrushSource()
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
