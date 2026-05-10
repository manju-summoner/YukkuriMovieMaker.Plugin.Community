using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Kagome
{
    internal class KagomeBrushSource(IGraphicsDevicesAndContext devices, KagomeBrushParameter parameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new InvalidOperationException(
            $"{nameof(Update)} must be called before accessing {nameof(Brush)}.");

        readonly DisposeCollector disposer = new();

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        bool isFirst = true;
        bool weaveEnabled;
        System.Windows.Media.Color color, outlineColor, backgroundColor;
        double size, lineWidth, outlineWidth;
        int bitmapWidth, bitmapHeight;
        Matrix3x2 matrix;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var newWeaveEnabled = parameter.WeaveEnabled;
            var newColor = parameter.Color;
            var newOutlineColor = parameter.OutlineColor;
            var newBackgroundColor = parameter.BackgroundColor;
            var zoom = parameter.Zoom.GetValue(frame, length, fps) / 100.0;
            var newSize = parameter.Size.GetValue(frame, length, fps) * zoom;
            var newLineWidth = parameter.LineWidth.GetValue(frame, length, fps) * zoom;
            var newOutlineWidth = parameter.OutlineWidth.GetValue(frame, length, fps) * zoom;
            var newMatrix = parameter.CreateBrushMatrix(desc);

            var dc = devices.DeviceContext;
            var factory = devices.D2D.Factory;

            var sqrt3 = MathF.Sqrt(3f);
            var r = (float)Math.Max(1.0, newSize);

            var tileW = r * 4f / sqrt3;
            var tileH = r * 2f;

            var scaledLineWidth = (float)Math.Max(0.1, newLineWidth);
            var scaledOutlineWidth = (float)Math.Max(0.0, newOutlineWidth);

            var maximumBitmapSize = dc.MaximumBitmapSize;
            var bitmapScale = Math.Min(1.0, (double)maximumBitmapSize / Math.Max(tileW, tileH));
            if (bitmapScale < 1.0)
            {
                tileW = (float)(tileW * bitmapScale);
                tileH = (float)(tileH * bitmapScale);
                scaledLineWidth = (float)(scaledLineWidth * bitmapScale);
                scaledOutlineWidth = (float)(scaledOutlineWidth * bitmapScale);
                newMatrix *= Matrix3x2.CreateScale((float)(1.0 / bitmapScale));
            }

            var newBitmapWidth = Math.Max(1, (int)Math.Ceiling(tileW));
            var newBitmapHeight = Math.Max(1, (int)Math.Ceiling(tileH));

            var bitmapNeedsUpdate = isFirst
                || this.weaveEnabled != newWeaveEnabled
                || !this.color.Equals(newColor)
                || !this.outlineColor.Equals(newOutlineColor)
                || !this.backgroundColor.Equals(newBackgroundColor)
                || this.size != newSize
                || this.lineWidth != newLineWidth
                || this.outlineWidth != newOutlineWidth
                || this.bitmapWidth != newBitmapWidth
                || this.bitmapHeight != newBitmapHeight;

            var isChanged = false;

            if (bitmapNeedsUpdate)
            {
                if (bitmap != null) disposer.RemoveAndDispose(ref bitmap);
                if (brush != null) disposer.RemoveAndDispose(ref brush);

                bitmap = dc.CreateNotInitializedBitmap(newBitmapWidth, newBitmapHeight);
                dc.Target = bitmap;
                try
                {
                    dc.BeginDraw();
                    dc.Clear(null);

                    using var bgBrush = dc.CreateSolidColorBrush(ToColor4(newBackgroundColor));
                    using var fillBrush = dc.CreateSolidColorBrush(ToColor4(newColor));
                    using var outlineBrush = dc.CreateSolidColorBrush(ToColor4(newOutlineColor));

                    dc.FillRectangle(new RawRectF(0f, 0f, newBitmapWidth, newBitmapHeight), bgBrush);

                    using var strokeStyle = factory.CreateStrokeStyle(new StrokeStyleProperties
                    {
                        StartCap = CapStyle.Round,
                        EndCap = CapStyle.Round,
                        LineJoin = LineJoin.Round,
                    });

                    DrawKagomeTile(
                        dc, factory, strokeStyle,
                        tileW, tileH,
                        scaledLineWidth, scaledOutlineWidth,
                        fillBrush, outlineBrush,
                        newWeaveEnabled);

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
            this.weaveEnabled = newWeaveEnabled;
            this.color = newColor;
            this.outlineColor = newOutlineColor;
            this.backgroundColor = newBackgroundColor;
            this.size = newSize;
            this.lineWidth = newLineWidth;
            this.outlineWidth = newOutlineWidth;
            this.bitmapWidth = newBitmapWidth;
            this.bitmapHeight = newBitmapHeight;
            this.matrix = newMatrix;

            return isChanged;
        }

        static void DrawKagomeTile(
            ID2D1DeviceContext dc,
            ID2D1Factory1 factory,
            ID2D1StrokeStyle strokeStyle,
            float tileW,
            float tileH,
            float lineWidth,
            float outlineWidth,
            ID2D1SolidColorBrush fillBrush,
            ID2D1SolidColorBrush outlineBrush,
            bool weaveEnabled)
        {
            var hasOutline = outlineWidth > 0f;
            var fullWidth = lineWidth + 2f * outlineWidth;

            var hSegs = new List<(Vector2, Vector2)>();
            for (int m = -2; m <= 4; m++)
            {
                float y = m * tileH * 0.5f;
                hSegs.Add((new Vector2(-tileW * 2f, y), new Vector2(tileW * 3f, y)));
            }

            var d1Segs = new List<(Vector2, Vector2)>();
            for (int k = -4; k <= 6; k++)
            {
                var p0 = new Vector2(k * tileW * 0.5f, tileH);
                var dir = new Vector2(tileW * 0.5f, -tileH);
                d1Segs.Add((p0 - dir * 4f, p0 + dir * 4f));
            }

            var d2Segs = new List<(Vector2, Vector2)>();
            for (int k = -4; k <= 6; k++)
            {
                var p0 = new Vector2(k * tileW * 0.5f, tileH);
                var dir = new Vector2(tileW * 0.5f, tileH);
                d2Segs.Add((p0 - dir * 4f, p0 + dir * 4f));
            }

            if (!weaveEnabled)
            {
                using var all = CreateGeometryGroup(factory, [.. hSegs, .. d1Segs, .. d2Segs]);
                if (hasOutline) dc.DrawGeometry(all, outlineBrush, fullWidth, strokeStyle);
                dc.DrawGeometry(all, fillBrush, lineWidth, strokeStyle);
            }
            else
            {
                using var phase1 = CreateGeometryGroup(factory, [.. hSegs]);
                if (hasOutline) dc.DrawGeometry(phase1, outlineBrush, fullWidth, strokeStyle);
                dc.DrawGeometry(phase1, fillBrush, lineWidth, strokeStyle);

                using var phase2 = CreateGeometryGroup(factory, [.. d1Segs]);
                if (hasOutline) dc.DrawGeometry(phase2, outlineBrush, fullWidth, strokeStyle);
                dc.DrawGeometry(phase2, fillBrush, lineWidth, strokeStyle);

                using var phase3 = CreateGeometryGroup(factory, [.. d2Segs]);
                if (hasOutline) dc.DrawGeometry(phase3, outlineBrush, fullWidth, strokeStyle);
                dc.DrawGeometry(phase3, fillBrush, lineWidth, strokeStyle);
            }
        }

        static ID2D1GeometryGroup CreateGeometryGroup(
            ID2D1Factory1 factory,
            params (Vector2 From, Vector2 To)[] segments)
        {
            var created = new List<ID2D1Geometry>(segments.Length);
            try
            {
                foreach (var (from, to) in segments)
                {
                    var path = factory.CreatePathGeometry();
                    created.Add(path);
                    using var sink = path.Open();
                    sink.BeginFigure(from, FigureBegin.Hollow);
                    sink.AddLine(to);
                    sink.EndFigure(FigureEnd.Open);
                    sink.Close();
                }
                return factory.CreateGeometryGroup(FillMode.Winding, [.. created]);
            }
            finally
            {
                foreach (var g in created)
                    g.Dispose();
            }
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
        // ~KagomeBrushSource()
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
