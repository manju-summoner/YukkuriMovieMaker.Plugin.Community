using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using D2DEffects = Vortice.Direct2D1.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Shape.LensFlare
{
    class LensFlareShapeParameterSource : IShapeSource2
    {
        readonly DisposeCollector disposer = new();
        public ID2D1Image Output { get; }
        public IEnumerable<VideoController> Controllers { get; private set; } = [];

        readonly LensFlareShapeParameter parameter;

#pragma warning disable IDE0044 // 読み取り専用修飾子を追加します
        D2DEffects.Flood floodEffect;
        D2DEffects.Crop cropEffect;
        LensFlareCustomEffect? lensFlareEffect;
#pragma warning restore IDE0044 // 読み取り専用修飾子を追加します

        bool isFirst = true;
        double x, y, intensity, scale, bladeCount, rotation, starLength, starBrightness,
            starWidth, streakBrightness, streakWidth, shimmerBrightness,
            ghostCount, ghostBrightness, haloRadius, haloBrightness, dispersion, seed;
        Color lightColor;
        System.Drawing.Size screenSize;

        public LensFlareShapeParameterSource(IGraphicsDevicesAndContext devices, LensFlareShapeParameter parameter)
        {
            this.parameter = parameter;

            floodEffect = new D2DEffects.Flood(devices.DeviceContext);
            disposer.Collect(floodEffect);
            cropEffect = new D2DEffects.Crop(devices.DeviceContext);
            disposer.Collect(cropEffect);
            lensFlareEffect = new LensFlareCustomEffect(devices);
            disposer.Collect(lensFlareEffect);

            floodEffect.Color = new Vector4(0f, 0f, 0f, 0f);

            if (!lensFlareEffect.IsEnabled)
            {
                //ShaderModel非対応環境では透明画像を出力する
                disposer.RemoveAndDispose(ref lensFlareEffect);

                cropEffect.Rectangle = new Vector4(-1, -1, 2, 2);
                using (var output = floodEffect.Output)
                    cropEffect.SetInput(0, output, true);
            }
            else
            {
                using (var output = floodEffect.Output)
                    lensFlareEffect.SetInput(0, output, true);
                using (var output = lensFlareEffect.Output)
                    cropEffect.SetInput(0, output, true);
            }

            var result = cropEffect.Output;
            disposer.Collect(result);
            Output = result;
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var x = parameter.X.GetValue(frame, length, fps);
            var y = parameter.Y.GetValue(frame, length, fps);

            //光源位置はプレビュー上のコントローラーでドラッグ操作できる
            Controllers =
            [
                new VideoController(
                [
                    new ControllerPoint(
                        new Vector3((float)x, (float)y, 0),
                        arg =>
                        {
                            parameter.X.AddToEachValues(arg.Delta.X);
                            parameter.Y.AddToEachValues(arg.Delta.Y);
                        })
                ])
            ];

            if (lensFlareEffect is null)
                return;

            var intensity = parameter.Intensity.GetValue(frame, length, fps);
            var scale = parameter.Scale.GetValue(frame, length, fps);
            var lightColor = parameter.LightColor;
            var bladeCount = Math.Round(parameter.BladeCount.GetValue(frame, length, fps));
            var rotation = parameter.Rotation.GetValue(frame, length, fps);
            var starLength = parameter.StarLength.GetValue(frame, length, fps);
            var starBrightness = parameter.StarBrightness.GetValue(frame, length, fps);
            var starWidth = parameter.StarWidth.GetValue(frame, length, fps);
            var streakBrightness = parameter.StreakBrightness.GetValue(frame, length, fps);
            var streakWidth = parameter.StreakWidth.GetValue(frame, length, fps);
            var shimmerBrightness = parameter.ShimmerBrightness.GetValue(frame, length, fps);
            var ghostCount = Math.Round(parameter.GhostCount.GetValue(frame, length, fps));
            var ghostBrightness = parameter.GhostBrightness.GetValue(frame, length, fps);
            var haloRadius = parameter.HaloRadius.GetValue(frame, length, fps);
            var haloBrightness = parameter.HaloBrightness.GetValue(frame, length, fps);
            var dispersion = parameter.Dispersion.GetValue(frame, length, fps);
            var seed = Math.Round(parameter.Seed.GetValue(frame, length, fps));
            var screenSize = desc.ScreenSize;

            if (isFirst || this.screenSize != screenSize)
            {
                cropEffect.Rectangle = new Vector4(
                    -screenSize.Width / 2f,
                    -screenSize.Height / 2f,
                    screenSize.Width / 2f,
                    screenSize.Height / 2f);
                lensFlareEffect.CanvasWidth = screenSize.Width;
                lensFlareEffect.CanvasHeight = screenSize.Height;
            }
            if (isFirst || this.x != x)
                lensFlareEffect.LightX = (float)x;
            if (isFirst || this.y != y)
                lensFlareEffect.LightY = (float)y;
            if (isFirst || this.intensity != intensity)
                lensFlareEffect.Intensity = (float)(intensity / 100);
            if (isFirst || this.scale != scale)
                lensFlareEffect.Scale = (float)(scale / 100);
            if (isFirst || this.lightColor != lightColor)
            {
                lensFlareEffect.ColorR = lightColor.R / 255f * lightColor.A / 255f;
                lensFlareEffect.ColorG = lightColor.G / 255f * lightColor.A / 255f;
                lensFlareEffect.ColorB = lightColor.B / 255f * lightColor.A / 255f;
            }
            if (isFirst || this.bladeCount != bladeCount)
                lensFlareEffect.Blades = (float)bladeCount;
            if (isFirst || this.rotation != rotation)
                lensFlareEffect.Rotation = (float)(rotation / 180 * Math.PI);
            if (isFirst || this.starLength != starLength)
                lensFlareEffect.StarLength = (float)(starLength / 100);
            if (isFirst || this.starBrightness != starBrightness)
                lensFlareEffect.StarBrightness = (float)(starBrightness / 100);
            if (isFirst || this.starWidth != starWidth)
                lensFlareEffect.StarWidth = (float)(starWidth / 100);
            if (isFirst || this.streakBrightness != streakBrightness)
                lensFlareEffect.StreakBrightness = (float)(streakBrightness / 100);
            if (isFirst || this.streakWidth != streakWidth)
                lensFlareEffect.StreakWidth = (float)(streakWidth / 100);
            if (isFirst || this.shimmerBrightness != shimmerBrightness)
                lensFlareEffect.ShimmerBrightness = (float)(shimmerBrightness / 100);
            if (isFirst || this.ghostCount != ghostCount)
                lensFlareEffect.GhostCount = (float)ghostCount;
            if (isFirst || this.ghostBrightness != ghostBrightness)
                lensFlareEffect.GhostBrightness = (float)(ghostBrightness / 100);
            if (isFirst || this.haloRadius != haloRadius)
                lensFlareEffect.HaloRadius = (float)(haloRadius / 100);
            if (isFirst || this.haloBrightness != haloBrightness)
                lensFlareEffect.HaloBrightness = (float)(haloBrightness / 100);
            if (isFirst || this.dispersion != dispersion)
                lensFlareEffect.Dispersion = (float)(dispersion / 100);
            if (isFirst || this.seed != seed)
                lensFlareEffect.Seed = (float)seed;

            isFirst = false;
            this.x = x;
            this.y = y;
            this.intensity = intensity;
            this.scale = scale;
            this.lightColor = lightColor;
            this.bladeCount = bladeCount;
            this.rotation = rotation;
            this.starLength = starLength;
            this.starBrightness = starBrightness;
            this.starWidth = starWidth;
            this.streakBrightness = streakBrightness;
            this.streakWidth = streakWidth;
            this.shimmerBrightness = shimmerBrightness;
            this.ghostCount = ghostCount;
            this.ghostBrightness = ghostBrightness;
            this.haloRadius = haloRadius;
            this.haloBrightness = haloBrightness;
            this.dispersion = dispersion;
            this.seed = seed;
            this.screenSize = screenSize;
        }

        #region IDisposable
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposer?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
