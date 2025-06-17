using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VignetteBlur
{
    internal class VignetteBlurProcessor(IGraphicsDevicesAndContext devices, VignetteBlurEffect item) : VideoEffectProcessorBase(devices)
    {
        VignetteBlurCustomEffect? effect;
        readonly List<GaussianBlur> blurs = [];
        Flood? transparent;

        bool isFirst = true;
        double x, y, radius, aspect, softness, blur, lightness, colorShift;
        bool isFixedSize;
        int inputCount;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null || transparent is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var x = item.X.GetValue(frame, length, fps);
            var y = item.Y.GetValue(frame, length, fps);
            var radius = item.Radius.GetValue(frame, length, fps);
            var aspect = item.Aspect.GetValue(frame, length, fps) / 100;
            var softness = item.Softness.GetValue(frame, length, fps);
            var isFixedSize = item.IsFixedSize;

            var blur = item.Blur.GetValue(frame, length, fps);
            var lightness = item.Lightness.GetValue(frame, length, fps) / 100;
            var colorShift = item.ColorShift.GetValue(frame, length, fps) / 100;
            var inputCount = (int)(Math.Max(0, Math.Log2(blur) + 1) + 2);

            if(isFirst || this.x != x || this.y != y)
                effect.Center = new((float)x, (float)y);
            if(isFirst || this.radius != radius)
                effect.Radius = (float)radius;
            if(isFirst || this.aspect != aspect)
                effect.Aspect = (float)aspect;
            if (isFirst || this.softness != softness)
                effect.Softness = (float)softness;
            if (isFirst || this.blur != blur)
                effect.Blur = (float)blur;
            if (isFirst || this.lightness != lightness)
                effect.Lightness = (float)lightness;
            if (isFirst || this.colorShift != colorShift)
                effect.ColorShift = (float)colorShift;
            if (isFirst || this.inputCount != inputCount)
            {
                //軽量化のため、使用しないぼかしエフェクトは透明画像に置き換える
                for (int i = 1; i < blurs.Count + 1; i++)
                {
                    using var output = i < inputCount ? blurs[i - 1].Output : transparent.Output;
                    effect.SetInput(i, output, true);
                }
            }
            if (isFirst || this.isFixedSize != isFixedSize)
            {
                effect.IsFixedSize = isFixedSize;
                foreach(var effect in blurs)
                    effect.BorderMode = isFixedSize ? BorderMode.Hard : BorderMode.Soft;
            }

            var controller = new VideoEffectController(
                item,
                [
                    new(
                        new((float)x, (float)y, 0),
                        arg=>
                        {
                            item.X.AddToEachValues(arg.Delta.X);
                            item.Y.AddToEachValues(arg.Delta.Y);
                        })]);

            isFirst = false;
            this.x = x;
            this.y = y;
            this.radius = radius;
            this.aspect = aspect;
            this.softness = softness;
            this.isFixedSize = isFixedSize;

            this.blur = blur;
            this.lightness = lightness;
            this.colorShift = colorShift;
            this.inputCount = inputCount;

            var desc = effectDescription.DrawDescription;
            return desc with { Controllers = [..desc.Controllers, controller] };
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new(devices);
            if(!effect.IsEnabled)
            {
                effect.Dispose();
                effect = null;
                return null;
            }
            disposer.Collect(effect);

            for(int i=0;i < effect.InputCount - 1;i++)
            {
                var blur = new GaussianBlur(devices.DeviceContext) 
                {
                    StandardDeviation = MathF.Pow(2, i) / 3f
                };
                blurs.Add(blur);
                disposer.Collect(blur);
            }

            transparent = new Flood(devices.DeviceContext);
            disposer.Collect(transparent);

            var result = effect.Output;
            disposer.Collect(result);

            return result;
        }

        protected override void ClearEffectChain()
        {
            effect?.SetInput(0, null, true);
            for(int i = 0; i < blurs.Count; i++)
            {
                effect?.SetInput(i + 1, null, true);
            }
        }

        protected override void setInput(ID2D1Image? input)
        {
            effect?.SetInput(0, input, true);
            foreach(var blur in blurs)
            {
                blur.SetInput(0, input, true);
            }
        }
    }
}