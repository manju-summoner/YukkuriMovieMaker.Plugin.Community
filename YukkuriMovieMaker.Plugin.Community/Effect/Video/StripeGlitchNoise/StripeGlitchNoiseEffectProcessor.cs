using MathNet.Numerics.Random;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.StripeGlitchNoise
{
    public class StripeGlitchNoiseEffectProcessor(IGraphicsDevicesAndContext devices, StripeGlitchNoiseEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;
        StripeGlitchNoiseCustomEffect? effect;

        bool isFirst = true, isHardBorder;
        int seed, stripeCount, repeat;
        float inputTop, inputHeight, stripeMaxWidth, stripeMaxShift, colorMaxShift, stripeMaxWidthAttenuation, stripeMaxShiftAttenuation;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var playbackRate = item.PlaybackRate.GetValue(frame, length, fps);
            var probability = item.Probability.GetValue(frame, length, fps);
            var seed = item.GetHashCode() % 1000 + (int)(frame * playbackRate / 100);

            bool isEnable = new MersenneTwister(seed).NextDouble() <= probability / 100;

            var bounds = devices.DeviceContext.GetImageLocalBounds(input);
            var inputTop = bounds.Top;
            var inputHeight = bounds.Bottom - bounds.Top;
            var stripeCount = isEnable ? (int)item.StripeCount.GetValue(frame, length, fps) : 0;
            var stripeMaxWidth = isEnable ? (float)item.StripeMaxWidth.GetValue(frame, length, fps) : 0;
            var stripeMaxShift = isEnable ? (float)item.StripeMaxShift.GetValue(frame, length, fps) : 0;
            var colorMaxShift = isEnable ? (float)item.ColorMaxShift.GetValue(frame, length, fps) : 0;
            var isHardBorder = item.IsHardBorderMode;

            var repeat = isEnable ? (int)item.Repeat.GetValue(frame, length, fps) : 0;
            var stripeMaxWidthAttenuation = isEnable ? (float)item.StripeMaxWidthAttenuation.GetValue(frame, length, fps) / 100 : 0;
            var stripeMaxShiftAttenuation = isEnable ? (float)item.StripeMaxShiftAttenuation.GetValue(frame, length, fps) / 100 : 0;

            if (isFirst || this.seed != seed)
                effect.Seed = seed;
            if (isFirst || this.inputTop != inputTop)
                effect.InputTop = inputTop;
            if (isFirst || this.inputHeight != inputHeight)
                effect.InputHeight = inputHeight;
            if (isFirst || this.stripeCount != stripeCount)

                effect.StripeCount = stripeCount;
            if (isFirst || this.stripeMaxWidth != stripeMaxWidth)
                effect.StripeMaxWidth = stripeMaxWidth;
            if (isFirst || this.stripeMaxShift != stripeMaxShift)
                effect.StripeMaxShift = stripeMaxShift;

            if (isFirst || this.colorMaxShift != colorMaxShift)
                effect.ColorMaxShift = colorMaxShift;
            if (isFirst || this.isHardBorder != isHardBorder)
                effect.IsHardBorder = isHardBorder;

            if (isFirst || this.repeat != repeat)
                effect.Repeat = repeat;
            if (isFirst || this.stripeMaxWidthAttenuation != stripeMaxWidthAttenuation)
                effect.StripeWidthAttenuation = stripeMaxWidthAttenuation;
            if (isFirst || this.stripeMaxShiftAttenuation != stripeMaxShiftAttenuation)
                effect.StripeMaxShiftAttenuation = stripeMaxShiftAttenuation;

            isFirst = false;
            this.seed = seed;
            this.inputTop = inputTop;
            this.inputHeight = inputHeight;
            this.stripeCount = stripeCount;
            this.stripeMaxWidth = stripeMaxWidth;
            this.stripeMaxShift = stripeMaxShift;
            this.colorMaxShift = colorMaxShift;
            this.isHardBorder = isHardBorder;
            this.repeat = repeat;
            this.stripeMaxWidthAttenuation = stripeMaxWidthAttenuation;
            this.stripeMaxShiftAttenuation = stripeMaxShiftAttenuation;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new StripeGlitchNoiseCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect?.Dispose();
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