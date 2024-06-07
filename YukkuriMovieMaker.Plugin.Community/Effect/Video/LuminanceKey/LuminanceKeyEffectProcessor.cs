using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuminanceKey
{
    internal class LuminanceKeyEffectProcessor(IGraphicsDevicesAndContext devices, LuminanceKeyEffect item) : VideoEffectProcessorBase(devices)
    {
        LuminanceKeyCustomEffect? effect;

        bool isFirst = true;
        double threshold;
        double smoothness;
        int mode;
        bool isInvert;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var threshold = item.Threshold.GetValue(frame, length, fps) / 100;
            var smoothness = item.Smoothness.GetValue(frame, length, fps) / 100;
            var mode = (int)item.Mode;
            var isInvert = item.IsInvert;

            if (isFirst || this.threshold != threshold)
                effect.Threshold = (float)threshold;
            if (isFirst || this.smoothness != smoothness)
                effect.Smoothness = (float)smoothness;
            if (isFirst || this.mode != mode)
                effect.Mode = mode;
            if (isFirst || this.isInvert != isInvert)
                effect.IsInvert = isInvert ? 1 : 0;

            isFirst = false;
            this.threshold = threshold;
            this.smoothness = smoothness;
            this.mode = mode;
            this.isInvert = isInvert;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new LuminanceKeyCustomEffect(devices);
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