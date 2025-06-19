using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using Vortice.Mathematics;
using Vortice;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Bloom
{
    internal class BloomEffectProcessor(IGraphicsDevicesAndContext devices, BloomEffect item) : VideoEffectProcessorBase(devices)
    {
        LuminanceKey.LuminanceKeyCustomEffect? luminanceKey;
        ColorMatrix? colorize;
        GaussianBlur? blur;
        ColorMatrix? opacity;
        Composite? composite;

        bool isFirst = true;
        double strength, threshold, radius;
        bool isColorizationEnabled, isFixedSizeEnabled;
        System.Windows.Media.Color color;

        public override DrawDescription Update(EffectDescription desc)
        {
            if (luminanceKey is null || blur is null || colorize is null || opacity is null || composite is null)
                return desc.DrawDescription;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var strength = item.Strength.GetValue(frame,length, fps) / 100;
            var threshold = item.Threshold.GetValue(frame, length, fps) / 100;
            var radius = item.Blur.GetValue(frame, length, fps);
            var isColorizationEnabled = item.IsColorizationEnabled;
            var color = item.Color;
            var isFixedSizeEnabled = item.IsFixedSizeEnabled;

            if (isFirst || this.threshold != threshold)
                luminanceKey.Threshold = (float)threshold;
            if (isFirst || this.isColorizationEnabled != isColorizationEnabled || this.color != color)
            {
                colorize.Matrix = isColorizationEnabled
                    ? new Matrix5x4() 
                    {
                        M11 = 0, M12 = 0, M13 = 0, M14 = 0,
                        M21 = 0, M22 = 0, M23 = 0, M24 = 0,
                        M31 = 0, M32 = 0, M33 = 0, M34 = 0,
                        M41 = (float)(color.R / 255d * color.A / 255d), M42 = (float)(color.G / 255d * color.A / 255d), M43 = (float)(color.B / 255d * color.A / 255d), M44 = (float)(color.A / 255d),
                        M51 = 0, M52 = 0, M53 = 0, M54 = 0,
                    }
                    : new Matrix5x4() { M11 = 1, M22 = 1, M33 = 1, M44 = 1 };
            }
            if(isFirst || this.radius != radius)
                blur.StandardDeviation = (float)(radius / 3);
            if (isFirst || this.strength != strength) 
            { 
                opacity.Matrix = new Matrix5x4() 
                {
                     M11 = (float)strength, M12 = 0, M13 = 0, M14 = 0,
                     M21 = 0, M22 = (float)strength, M23 = 0, M24 = 0,
                     M31 = 0, M32 = 0, M33 = (float)strength, M34 = 0,
                     M41 = 0, M42 = 0, M43 = 0, M44 = (float)strength,
                     M51 = 0, M52 = 0, M53 = 0, M54 = 0,
                };
            }
            if (isFirst || this.isFixedSizeEnabled != isFixedSizeEnabled)
                blur.BorderMode = isFixedSizeEnabled ? BorderMode.Hard : BorderMode.Soft;

            isFirst = false;
            this.strength = strength;
            this.threshold = threshold;
            this.radius = radius;
            this.isColorizationEnabled = isColorizationEnabled;
            this.color = color;
            this.isFixedSizeEnabled = isFixedSizeEnabled;

            return desc.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            luminanceKey = new LuminanceKey.LuminanceKeyCustomEffect(devices);
            if(!luminanceKey.IsEnabled)
            {
                luminanceKey.Dispose();
                luminanceKey = null;
                return null;
            }
            luminanceKey.Smoothness = 0;
            disposer.Collect(luminanceKey);

            colorize = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(colorize);

            blur = new GaussianBlur(devices.DeviceContext)
            { 
                Optimization = GaussianBlurOptimization.Quality
            };
            disposer.Collect(blur);

            opacity = new ColorMatrix(devices.DeviceContext);
            disposer.Collect(opacity);

            composite = new Composite(devices.DeviceContext)
            { 
                InputCount = 2,
                Mode = CompositeMode.Plus,
            };
            disposer.Collect(composite);

            using (var output = luminanceKey.Output)
                colorize.SetInput(0, output, true);
            using (var output = colorize.Output)
                blur.SetInput(0, output, true);
            using (var output = blur.Output)
                opacity.SetInput(0, output, true);
            using (var output = opacity.Output)
                composite.SetInput(1, output, true);

            var result = composite.Output;
            disposer.Collect(result);
            return result;
        }

        protected override void ClearEffectChain()
        {
            luminanceKey?.SetInput(0, null, true);
            blur?.SetInput(0, null, true);
            composite?.SetInput(0, null, true);
            composite?.SetInput(1, null, true);
        }

        protected override void setInput(ID2D1Image? input)
        {
            luminanceKey?.SetInput(0, input, true);
            composite?.SetInput(0, input, true);
        }
    }
}