using Vortice.Direct2D1;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GodRay
{
    internal class GodRayEffectProcessor(IGraphicsDevicesAndContext devices, GodRayEffect item) : VideoEffectProcessorBase(devices)
    {
        GodRayCustomEffect? effect;

        bool isFirst = true;
        double lightX, lightY, intensity, decay, density, weight, samples, threshold;
        Color lightColor;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var lightX = item.LightX.GetValue(frame, length, fps) / 100.0;
            var lightY = item.LightY.GetValue(frame, length, fps) / 100.0;
            var intensity = item.Intensity.GetValue(frame, length, fps);
            var decay = item.Decay.GetValue(frame, length, fps);
            var density = item.Density.GetValue(frame, length, fps);
            var weight = item.Weight.GetValue(frame, length, fps);
            var samples = item.Samples.GetValue(frame, length, fps);
            var threshold = item.Threshold.GetValue(frame, length, fps);
            var lightColor = item.LightColor;

            if (isFirst || this.lightX != lightX || this.lightY != lightY ||
                this.intensity != intensity || this.decay != decay ||
                this.density != density || this.weight != weight ||
                this.samples != samples || this.threshold != threshold ||
                this.lightColor != lightColor)
            {
                effect.LightX = (float)lightX;
                effect.LightY = (float)lightY;
                effect.Intensity = (float)intensity;
                effect.Decay = (float)decay;
                effect.Density = (float)density;
                effect.Weight = (float)weight;
                effect.Samples = (float)samples;
                effect.Threshold = (float)threshold;
                effect.ColorR = lightColor.R / 255f;
                effect.ColorG = lightColor.G / 255f;
                effect.ColorB = lightColor.B / 255f;
                effect.ColorA = lightColor.A / 255f;
            }

            isFirst = false;
            this.lightX = lightX;
            this.lightY = lightY;
            this.intensity = intensity;
            this.decay = decay;
            this.density = density;
            this.weight = weight;
            this.samples = samples;
            this.threshold = threshold;
            this.lightColor = lightColor;

            return effectDescription.DrawDescription;
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new GodRayCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            var output = effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
        }
    }
}
