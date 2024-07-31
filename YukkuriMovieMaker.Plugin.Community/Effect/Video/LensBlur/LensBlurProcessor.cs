using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LensBlur
{
    internal class LensBlurProcessor(IGraphicsDevicesAndContext devices, LensBlurEffect item) : VideoEffectProcessorBase(devices)
    {
        LensBlurCustomEffect? effect;

        bool isFirst = true;
        double radius, brightness, edgeStrength, quality;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var radius = item.BlurRadius.GetValue(frame, length, fps);
            var brightness = item.Brightness.GetValue(frame, length, fps) / 100;
            var edgeStrength = item.EdgeStrength.GetValue(frame, length, fps);
            var quality = item.Quality.GetValue(frame, length, fps);

            if (isFirst || this.radius != radius)
                effect.Radius = (float)radius;
            if (isFirst || this.brightness != brightness)
                effect.Brightness = (float)brightness;
            if (isFirst || this.edgeStrength != edgeStrength)
                effect.EdgeStrength = (float)edgeStrength;
            if (isFirst || this.quality != quality)
                effect.Quality = (float)quality;

            isFirst = false;
            this.radius = radius;
            this.brightness = brightness;
            this.edgeStrength = edgeStrength;
            this.quality = quality;

            return effectDescription.DrawDescription;
        }
        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new (devices);
            if(!effect.IsEnabled)
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

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }

    }
}