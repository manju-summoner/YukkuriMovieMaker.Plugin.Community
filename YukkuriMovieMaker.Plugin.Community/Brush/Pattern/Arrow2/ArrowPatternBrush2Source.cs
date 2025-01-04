using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Pattern.Arrow2
{
    internal class ArrowPatternBrush2Source(IGraphicsDevicesAndContext devices, ArrowPatternBrush2Parameter arrowPatternBrush2Parameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new NullReferenceException(nameof(brush));
        readonly DisposeCollector disposer = new();

        bool isFirst = true;
        System.Windows.Media.Color color, backgroundColor;
        double width, height, point;
        Matrix3x2 matrix;

        ID2D1Bitmap? bitmap;
        ID2D1BitmapBrush? brush;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var isChanged = false;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var color = arrowPatternBrush2Parameter.Color;
            var backgroundColor = arrowPatternBrush2Parameter.BackgroundColor;
            var zoom = arrowPatternBrush2Parameter.Zoom.GetValue(frame, length, fps) / 100f;
            var width = arrowPatternBrush2Parameter.Width.GetValue(frame, length, fps) * zoom;
            var height = arrowPatternBrush2Parameter.Height.GetValue(frame, length, fps) * zoom;
            var point = arrowPatternBrush2Parameter.Point.GetValue(frame, length, fps) * zoom;
            var matrix = arrowPatternBrush2Parameter.CreateBrushMatrix(desc);

            var dc = devices.DeviceContext;

            if (isFirst || !this.color.Equals(color) || !this.backgroundColor.Equals(backgroundColor)
                || this.width != width || this.height != height || this.point != point)
            {
                var roundWidth = Math.Round(width);
                var roundHeight = Math.Round(height);

                using (var geometry = devices.D2D.Factory.CreatePathGeometry())
                {
                    using (var sink = geometry.Open())
                    {
                        for (int i = 0; i < 2 + (int)Math.Ceiling(point / height); i += 2)
                        {
                            sink.BeginFigure(new Vector2(0f, (float)(roundHeight * (i + 1) - point)), FigureBegin.Filled);
                            sink.AddLines([
                                new Vector2((float)roundWidth, (float)roundHeight * (i + 1)),
                                new Vector2((float)roundWidth, (float)roundHeight * i),
                                new Vector2(0f, (float)(roundHeight * i - point))]);
                            sink.EndFigure(FigureEnd.Closed);
                        }
                        for (int i = 1; i < 2 + (int)Math.Ceiling(point / height); i += 2)
                        {
                            sink.BeginFigure(new Vector2((float)roundWidth, (float)roundHeight * (i + 1)), FigureBegin.Filled);
                            sink.AddLines([
                                new Vector2((float)roundWidth * 2, (float)(roundHeight * (i + 1) - point)),
                                new Vector2((float)roundWidth * 2, (float)(roundHeight * i - point)),
                                new Vector2((float)(roundWidth), (float)roundHeight * i)]);
                            sink.EndFigure(FigureEnd.Closed);
                        }
                        sink.Close();
                    }

                    if (bitmap != null)
                        disposer.RemoveAndDispose(ref bitmap);
                    using (var solidColorBrush = dc.CreateSolidColorBrush(new Color(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f)))
                    {
                        bitmap = dc.CreateNotInitializedBitmap(Math.Max(1, (int)roundWidth * 2), Math.Max(1, (int)roundHeight * 2));
                        dc.Target = bitmap;
                        dc.BeginDraw();
                        dc.Clear(new Color(backgroundColor.R / 255.0f, backgroundColor.G / 255.0f, backgroundColor.B / 255.0f, backgroundColor.A / 255.0f));
                        dc.FillGeometry(geometry, solidColorBrush);
                        dc.EndDraw();
                        dc.Target = null;
                    }
                    disposer.Collect(bitmap);
                }

                isChanged = true;
            }

            if (isFirst || isChanged || !this.matrix.Equals(matrix))
            {
                if (brush != null)
                    disposer.RemoveAndDispose(ref brush);

                brush = dc.CreateBitmapBrush(
                    bitmap,
                    new BitmapBrushProperties1
                    {
                        ExtendModeX = ExtendMode.Wrap,
                        ExtendModeY = ExtendMode.Wrap,
                        InterpolationMode = InterpolationMode.MultiSampleLinear
                    },
                    new BrushProperties(1f, matrix));

                disposer.Collect(brush);

                isChanged = true;
            }

            isFirst = false;
            this.color = color;
            this.backgroundColor = backgroundColor;
            this.width = width;
            this.height = height;
            this.point = point;
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
