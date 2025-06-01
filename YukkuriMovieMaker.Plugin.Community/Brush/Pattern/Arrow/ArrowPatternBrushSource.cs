using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow
{
    internal class ArrowPatternBrushSource(IGraphicsDevicesAndContext devices, ArrowPatternBrushParameter arrowPatternBrushParameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new NullReferenceException(nameof(brush));
        readonly DisposeCollector disposer = new();

        bool isFirst = true;
        System.Windows.Media.Color color, backgroundColor;
        double featherWidth, shaftWidth, height, point;
        Matrix3x2 matrix;

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var isChanged = false;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var color = arrowPatternBrushParameter.Color;
            var backgroundColor = arrowPatternBrushParameter.BackgroundColor;
            var zoom = arrowPatternBrushParameter.Zoom.GetValue(frame, length, fps) / 100f;
            var featherWidth = arrowPatternBrushParameter.FeatherWidth.GetValue(frame, length, fps) * zoom;
            var shaftWidth = arrowPatternBrushParameter.ShaftWidth.GetValue(frame, length, fps) * zoom;
            var height = arrowPatternBrushParameter.Height.GetValue(frame, length, fps) * zoom;
            var point = arrowPatternBrushParameter.Point.GetValue(frame, length, fps) * zoom;
            var matrix = arrowPatternBrushParameter.CreateBrushMatrix(desc);

            var dc = devices.DeviceContext;

            var roundFeatherWidth = Math.Round(featherWidth);
            var roundShaftWidth = Math.Round(shaftWidth);
            var roundHeight = Math.Round(height);

            //テクスチャがBitmapの最大サイズを超える場合、Bitmap内に収まるように比率を保って縮小し、縮小分だけmatrixで引き延ばす
            var maximumBitmapSize = dc.MaximumBitmapSize;
            var bitmapWidth = Math.Max(1, (int)roundFeatherWidth * 4 + (int)roundShaftWidth * 2);
            var bitmapHeight = Math.Max(1, (int)roundHeight * 2);
            var bitmapScale = Math.Min(1, (double)maximumBitmapSize / Math.Max(bitmapWidth, bitmapHeight));
            if(bitmapScale < 1)
            {
                roundFeatherWidth = Math.Round(roundFeatherWidth * bitmapScale);
                roundShaftWidth = Math.Round(roundShaftWidth * bitmapScale);
                roundHeight = Math.Round(roundHeight * bitmapScale);
                bitmapWidth = Math.Clamp((int)(bitmapWidth * bitmapScale), 1, maximumBitmapSize);
                bitmapHeight = Math.Clamp((int)(bitmapHeight * bitmapScale), 1, maximumBitmapSize);
                point *= bitmapScale;
                matrix *= Matrix3x2.CreateScale((float)(1 / bitmapScale));
            }

            if (isFirst || !this.color.Equals(color) || !this.backgroundColor.Equals(backgroundColor)
                || this.featherWidth != featherWidth || this.shaftWidth != shaftWidth || this.height != height|| this.point != point)
            {
                using var geometry1 = devices.D2D.Factory.CreatePathGeometry();
                using (var sink = geometry1.Open())
                {
                    for (int i = 0; i < 2 + (int)Math.Ceiling(point / height); i += 2)
                    {
                        sink.BeginFigure(new Vector2(0f, (float)(roundHeight * (i + 1) - point)), FigureBegin.Filled);
                        sink.AddLines([
                            new Vector2((float)roundFeatherWidth, (float)roundHeight * (i + 1)),
                                new Vector2((float)roundFeatherWidth, (float)roundHeight * i),
                                new Vector2(0f, (float)(roundHeight * i - point))]);
                        sink.EndFigure(FigureEnd.Closed);
                        sink.BeginFigure(new Vector2((float)(roundFeatherWidth + roundShaftWidth), (float)roundHeight * (i + 1)), FigureBegin.Filled);
                        sink.AddLines([
                            new Vector2((float)(roundFeatherWidth * 2 + roundShaftWidth), (float)(roundHeight * (i + 1) - point)),
                                new Vector2((float)(roundFeatherWidth * 2 + roundShaftWidth), (float)(roundHeight * i - point)),
                                new Vector2((float)(roundFeatherWidth + roundShaftWidth), (float)roundHeight * i)]);
                        sink.EndFigure(FigureEnd.Closed);
                        sink.BeginFigure(new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth), (float)roundHeight * (i + 1)), FigureBegin.Filled);
                        sink.AddLines([
                            new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth * 2), (float)roundHeight * (i + 1)),
                                new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth * 2), (float)roundHeight * i),
                                new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth), (float)roundHeight * i)]);
                        sink.EndFigure(FigureEnd.Closed);
                    }
                    for (int i = 1; i < 2 + (int)Math.Ceiling(point / height); i += 2)
                    {
                        sink.BeginFigure(new Vector2((float)roundFeatherWidth, (float)roundHeight * (i + 1)), FigureBegin.Filled);
                        sink.AddLines([
                            new Vector2((float)(roundFeatherWidth + roundShaftWidth), (float)roundHeight * (i + 1)),
                                new Vector2((float)(roundFeatherWidth + roundShaftWidth), (float)roundHeight * i),
                                new Vector2((float)(roundFeatherWidth), (float)roundHeight * i)]);
                        sink.EndFigure(FigureEnd.Closed);
                        sink.BeginFigure(new Vector2((float)(roundFeatherWidth * 2 + roundShaftWidth), (float)(roundHeight * (i + 1) - point)), FigureBegin.Filled);
                        sink.AddLines([
                            new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth), (float)roundHeight * (i + 1)),
                                new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth), (float)roundHeight * i),
                                new Vector2((float)(roundFeatherWidth * 2 + roundShaftWidth), (float)(roundHeight * i - point))]);
                        sink.EndFigure(FigureEnd.Closed);
                        sink.BeginFigure(new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth * 2), (float)roundHeight * (i + 1)), FigureBegin.Filled);
                        sink.AddLines([
                            new Vector2((float)(roundFeatherWidth * 4 + roundShaftWidth * 2), (float)(roundHeight * (i + 1) - point)),
                                new Vector2((float)(roundFeatherWidth * 4 + roundShaftWidth * 2), (float)(roundHeight * i - point)),
                                new Vector2((float)(roundFeatherWidth * 3 + roundShaftWidth * 2), (float)roundHeight * i)]);
                        sink.EndFigure(FigureEnd.Closed);
                    }
                    sink.Close();
                }

                using var geometry2 = devices.D2D.Factory.CreatePathGeometry();
                using (var sink = geometry2.Open())
                {
                    using var rect = devices.D2D.Factory.CreateRectangleGeometry(new Vortice.RawRectF(0, 0, bitmapWidth, bitmapHeight));
                    rect.CombineWithGeometry(geometry1, CombineMode.Xor, sink);
                    sink.Close();
                }

                if (bitmap != null)
                    disposer.RemoveAndDispose(ref bitmap);
                using (var solidColorBrush1 = dc.CreateSolidColorBrush(new Color(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f)))
                using (var solidColorBrush2 = dc.CreateSolidColorBrush(new Color(backgroundColor.R / 255.0f, backgroundColor.G / 255.0f, backgroundColor.B / 255.0f, backgroundColor.A / 255.0f)))
                {
                    bitmap = dc.CreateNotInitializedBitmap(bitmapWidth, bitmapHeight);
                    dc.Target = bitmap;
                    dc.BeginDraw();
                    dc.Clear(null);
                    dc.FillGeometry(geometry1, solidColorBrush1);
                    dc.FillGeometry(geometry2, solidColorBrush2);
                    dc.EndDraw();
                    dc.Target = null;
                }
                disposer.Collect(bitmap);

                isChanged = true;
            }

            if (isFirst || isChanged || !this.matrix.Equals(matrix))
            {
                if(brush != null)
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
            this.featherWidth = featherWidth;
            this.height = height;
            this.point = point;
            this.shaftWidth = shaftWidth;
            this.matrix = matrix;

            return isChanged;
        }

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
        // ~RainbowBrushSource()
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
