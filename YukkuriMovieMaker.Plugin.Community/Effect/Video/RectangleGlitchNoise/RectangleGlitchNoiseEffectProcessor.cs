using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RectangleGlitchNoise
{
    public class RectangleGlitchNoiseEffectProcessor(IGraphicsDevicesAndContext devices, RectangleGlitchNoiseEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly IGraphicsDevicesAndContext devices = devices;
        RectangleGlitchNoiseCustomEffect? effect;

        bool isFirst = true, isClipping, isHardBorder;
        int seed, rectangleCount, repeat;
        float inputTop, inputLeft, inputWidth, inputHeight, rectangleMaxWidth, rectangleMaxHeight, rectangleMaxXShift, rectangleMaxYShift, colorMaxShift, rectangleMaxWidthAttenuation, rectangleMaxHeightAttenuation, rectangleMaxXShiftAttenuation, rectangleMaxYShiftAttenuation;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var playbackRate = item.PlaybackRate.GetValue(frame, length, fps);
            var seed = item.GetHashCode() % 1000 + (int)(frame * playbackRate / 100);
            var bounds = devices.DeviceContext.GetImageLocalBounds(input);
            var inputTop = bounds.Top;
            var inputLeft = bounds.Left;
            var inputWidth = bounds.Right - bounds.Left;
            var inputHeight = bounds.Bottom - bounds.Top;
            var rectangleCount = (int)item.RectangleCount.GetValue(frame, length, fps);
            var rectangleMaxWidth = (float)item.RectangleMaxWidth.GetValue(frame, length, fps);
            var rectangleMaxHeight = (float)item.RectangleMaxHeight.GetValue(frame, length, fps);
            var rectangleMaxXShift = (float)item.RectangleMaxXShift.GetValue(frame, length, fps);
            var rectangleMaxYShift = (float)item.RectangleMaxYShift.GetValue(frame, length, fps);
            var colorMaxShift = (float)item.ColorMaxShift.GetValue(frame, length, fps);
            var isClipping = item.IsClipping;
            var isHardBorder = item.IsHardBorderMode;

            var repeat = (int)item.Repeat.GetValue(frame, length, fps);
            var rectangleMaxWidthAttenuation = (float)item.RectangleMaxWidthAttenuation.GetValue(frame, length, fps) / 100;
            var rectangleMaxHeightAttenuation = (float)item.RectangleMaxHeightAttenuation.GetValue(frame, length, fps) / 100;
            var rectangleMaxXShiftAttenuation = (float)item.RectangleMaxXShiftAttenuation.GetValue(frame, length, fps) / 100;
            var rectangleMaxYShiftAttenuation = (float)item.RectangleMaxYShiftAttenuation.GetValue(frame, length, fps) / 100;

            if (isFirst || this.seed != seed)
                effect.Seed = seed;
            if (isFirst || this.inputTop != inputTop)
                effect.InputTop = inputTop;
            if (isFirst || this.inputLeft != inputLeft)
                effect.InputLeft = inputLeft;
            if (isFirst || this.inputWidth != inputWidth)
                effect.InputWidth = inputWidth;
            if (isFirst || this.inputHeight != inputHeight)
                effect.InputHeight = inputHeight;
            if (isFirst || this.rectangleCount != rectangleCount)
                effect.RectangleCount = rectangleCount;
            if (isFirst || this.rectangleMaxWidth != rectangleMaxWidth)
                effect.RectangleMaxWidth = rectangleMaxWidth;
            if (isFirst || this.rectangleMaxHeight != rectangleMaxHeight)
                effect.RectangleMaxHeight = rectangleMaxHeight;
            if (isFirst || this.rectangleMaxXShift != rectangleMaxXShift)
                effect.RectangleMaxXShift = rectangleMaxXShift;
            if (isFirst || this.rectangleMaxYShift != rectangleMaxYShift)
                effect.RectangleMaxYShift = rectangleMaxYShift;

            if (isFirst || this.colorMaxShift != colorMaxShift)
                effect.ColorMaxShift = colorMaxShift;
            if (isFirst || this.isClipping != isClipping)
                effect.IsClipping = isClipping;
            if (isFirst || this.isHardBorder != isHardBorder)
                effect.IsHardBorder = isHardBorder;

            if (isFirst || this.repeat != repeat)
                effect.Repeat = repeat;
            if (isFirst || this.rectangleMaxWidthAttenuation != rectangleMaxWidthAttenuation)
                effect.RectangleMaxWidthAttenuation = rectangleMaxWidthAttenuation;
            if (isFirst || this.rectangleMaxHeightAttenuation != rectangleMaxHeightAttenuation)
                effect.RectangleMaxHeightAttenuation = rectangleMaxHeightAttenuation;
            if (isFirst || this.rectangleMaxXShiftAttenuation != rectangleMaxXShiftAttenuation)
                effect.RectangleMaxXShiftAttenuation = rectangleMaxXShiftAttenuation;
            if (isFirst || this.rectangleMaxYShiftAttenuation != rectangleMaxYShiftAttenuation)
                effect.RectangleMaxYShiftAttenuation = rectangleMaxYShiftAttenuation;

            isFirst = false;
            this.seed = seed;
            this.inputTop = inputTop;
            this.inputLeft = inputLeft;
            this.inputWidth = inputWidth;
            this.inputHeight = inputHeight;
            this.rectangleCount = rectangleCount;
            this.rectangleMaxWidth = rectangleMaxWidth;
            this.rectangleMaxHeight = rectangleMaxHeight;
            this.rectangleMaxXShift = rectangleMaxXShift;
            this.rectangleMaxYShift = rectangleMaxYShift;
            this.colorMaxShift = colorMaxShift;
            this.isClipping = isClipping;
            this.isHardBorder = isHardBorder;
            this.repeat = repeat;
            this.rectangleMaxWidthAttenuation = rectangleMaxWidthAttenuation;
            this.rectangleMaxHeightAttenuation = rectangleMaxHeightAttenuation;
            this.rectangleMaxXShiftAttenuation = rectangleMaxXShiftAttenuation;
            this.rectangleMaxYShiftAttenuation = rectangleMaxYShiftAttenuation;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new RectangleGlitchNoiseCustomEffect(devices);
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
