using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Brush.Rainbow.Radial
{
    internal class RainbowRadialGradientBrushSource(IGraphicsDevicesAndContext devices, RainbowRadialGradientBrushParameter rainbowBrushParameter) : IBrushSource
    {
        public ID2D1Brush Brush => brush ?? throw new NullReferenceException(nameof(brush));
        readonly DisposeCollector disposer = new();

        bool isFirst = true, isInverted;
        double offset, centerX, centerY, radiusX, radiusY, originX, originY, saturation, brightness, angle;
        RainbowColorSpace colorSpace;
        Vortice.Direct2D1.ExtendMode extendMode;

        ID2D1GradientStopCollection? stopCollection;
        ID2D1RadialGradientBrush? brush;

        public bool Update(TimelineItemSourceDescription desc)
        {
            var isChanged = false;
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var offset = rainbowBrushParameter.Offset.GetValue(frame, length, fps);
            var zoom = rainbowBrushParameter.Zoom.GetValue(frame, length, fps) / 100;
            var angle = rainbowBrushParameter.Angle.GetValue(frame, length, fps) / 180 * Math.PI;
            var aspect = rainbowBrushParameter.Aspect.GetValue(frame, length, fps);
            var aspectZoomX = 1 - Math.Max(0, aspect);
            var aspectZoomY = 1 + Math.Min(0, aspect);
            var centerX = rainbowBrushParameter.CenterX.GetValue(frame, length, fps);
            var centerY = rainbowBrushParameter.CenterY.GetValue(frame, length, fps);
            var radiusX = rainbowBrushParameter.RadiusX.GetValue(frame, length, fps) * zoom * aspectZoomX;
            var radiusY = rainbowBrushParameter.RadiusY.GetValue(frame, length, fps) * zoom * aspectZoomY;
            var originX = rainbowBrushParameter.OriginX.GetValue(frame, length, fps) * zoom * aspectZoomX;
            var originY = rainbowBrushParameter.OriginY.GetValue(frame, length, fps) * zoom * aspectZoomY;
            var saturation = rainbowBrushParameter.Saturation.GetValue(frame, length, fps);
            var brightness = rainbowBrushParameter.Brightness.GetValue(frame, length, fps);
            var colorSpace = rainbowBrushParameter.ColorSpace;
            var extendMode = rainbowBrushParameter.ExtendMode.ToD2DExtendMode();
            var isInverted = rainbowBrushParameter.IsInverted;

            if (isFirst || this.offset != offset || this.saturation != saturation || this.brightness != brightness || this.colorSpace != colorSpace || this.extendMode != extendMode || this.angle != angle || this.isInverted != isInverted)
            {
                if (stopCollection != null)
                    disposer.RemoveAndDispose(ref stopCollection);
                stopCollection = devices.DeviceContext.CreateGradientStopCollection(
                    RainbowStopsGenerator.Create(-offset / 100, saturation/100, brightness/100, colorSpace),
                    Gamma.StandardRgb,
                    extendMode);
                disposer.Collect(stopCollection);

                if (brush != null)
                    disposer.RemoveAndDispose(ref brush);
                brush = devices.DeviceContext.CreateRadialGradientBrush(
                    new RadialGradientBrushProperties()
                    {
                        Center = new((float)centerX, (float)centerY),
                        GradientOriginOffset = new((float)originX, (float)originY),
                        RadiusX = (float)radiusX,
                        RadiusY = (float)radiusY,
                    },
                    new BrushProperties(
                        1f,
                        Matrix3x2.CreateRotation((float)angle)
                        * Matrix3x2.CreateScale(isInverted ? -1 : 1, 1)),
                    stopCollection);
                disposer.Collect(brush);

                isChanged = true;

            }
            if(isFirst || !isChanged && (this.centerX != centerX || this.centerY != centerY || this.originX != originX || this. originY != originY || this. radiusX != radiusX || this.radiusY != radiusY) )
            {
                if(brush is null)
                    throw new InvalidOperationException("brush is null");

                brush.Center = new((float)centerX, (float)centerY);
                brush.GradientOriginOffset = new((float)originX, (float)originY);
                brush.RadiusX = (float)radiusX;
                brush.RadiusY = (float)radiusY;

                isChanged = true;
            }

            isFirst = false;
            this.offset = offset;
            this.centerX = centerX;
            this.centerY = centerY;
            this.radiusX = radiusX;
            this.radiusY = radiusY;
            this.originX = originX;
            this.originY = originY;
            this.saturation = saturation;
            this.brightness = brightness;
            this.colorSpace = colorSpace;
            this.extendMode = extendMode;
            this.isInverted = isInverted;
            this.angle = angle;
            this.isInverted = isInverted;

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