using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Binarization
{
    public class BinarizationEffectProcessor(IGraphicsDevicesAndContext devices, BinarizationEffect binarizationEffect) : VideoEffectProcessorBase(devices)
    {
        bool isFirst = true;
        float threshold;
        bool isInverted, keepColor;

        BinarizationCustomEffect? effect;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var threshold = (float)binarizationEffect.Threshold.GetValue(frame, length, fps) / 100f;
            var isInverted = binarizationEffect.IsInverted;
            var keepColor = binarizationEffect.KeepColor;

            if (isFirst || this.threshold != threshold || this.isInverted != isInverted || this.keepColor != keepColor)
            {
                effect.Value = threshold;
                effect.IsInverted = isInverted;
                effect.KeepColor = keepColor;
            }

            isFirst = false;
            this.threshold = threshold;
            this.isInverted = isInverted;
            this.keepColor = keepColor;

            return effectDescription.DrawDescription;
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new BinarizationCustomEffect(devices);
            if (!effect.IsEnabled)
                return null;
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