using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow.Linear
{
    internal class RainbowLinearGradientBrushSource(IGraphicsDevicesAndContext devices, RainbowLinearGradientBrushParameter rainbowBrushParameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new NullReferenceException(nameof(brush));
        readonly DisposeCollector disposer = new();

        bool isFirst = true;
        double width, offset, saturation, brightness, angle;
        RainbowColorSpace colorSpace;
        Vortice.Direct2D1.ExtendMode extendMode;

        ID2D1GradientStopCollection? stopCollection;
        ID2D1LinearGradientBrush? brush;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var isChanged = false;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var width = Math.Max(0.001, rainbowBrushParameter.Width.GetValue(frame, length, fps));
            var offset = rainbowBrushParameter.Offset.GetValue(frame, length, fps);
            var saturation = rainbowBrushParameter.Saturation.GetValue(frame, length, fps);
            var brightness = rainbowBrushParameter.Brightness.GetValue(frame, length, fps);
            var angle = rainbowBrushParameter.Angle.GetValue(frame, length, fps);
            var colorSpace = rainbowBrushParameter.ColorSpace;
            var extendMode = rainbowBrushParameter.ExtendMode.ToD2DExtendMode();

            if (isFirst || this.saturation != saturation || this.brightness != brightness || this.colorSpace != colorSpace || this.extendMode != extendMode)
            {
                if (stopCollection != null)
                    disposer.RemoveAndDispose(ref stopCollection);
                stopCollection = devices.DeviceContext.CreateGradientStopCollection(
                    RainbowStopsGenerator.Create(saturation/100, brightness/100, colorSpace),
                    Gamma.StandardRgb,
                    extendMode);
                disposer.Collect(stopCollection);

                if (brush != null)
                    disposer.RemoveAndDispose(ref brush);
                brush = devices.DeviceContext.CreateLinearGradientBrush(
                    new LinearGradientBrushProperties
                    {
                        StartPoint = Vector2.Transform(new((float)-width / 2 + (float)offset, 0), Matrix3x2.CreateRotation((float)angle / 180 * MathF.PI)),
                        EndPoint = Vector2.Transform(new((float)width / 2 + (float)offset, 0), Matrix3x2.CreateRotation((float)angle / 180 * MathF.PI))
                    },
                    stopCollection);
                disposer.Collect(brush);

                isChanged = true;

            }
            if(isFirst || !isChanged && (this.width != width || this.angle != angle || this.offset != offset) )
            {
                if(brush is null)
                    throw new InvalidOperationException("brush is null");

                brush.StartPoint = Vector2.Transform(new((float)-width / 2 + (float)offset, 0), Matrix3x2.CreateRotation((float)angle / 180 * MathF.PI));
                brush.EndPoint = Vector2.Transform(new((float)width / 2 + (float)offset, 0), Matrix3x2.CreateRotation((float)angle / 180 * MathF.PI));
                isChanged = true;
            }

            isFirst = false;
            this.width = width;
            this.offset = offset;
            this.saturation = saturation;
            this.brightness = brightness;
            this.angle = angle;
            this.colorSpace = colorSpace;
            this.extendMode = extendMode;

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