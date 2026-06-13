using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Flip
{
    internal class FlipEffectProcessor(IGraphicsDevicesAndContext devices, FlipEffect flipEffect) : VideoEffectProcessorBase(devices)
    {
        AffineTransform2D? effect;
        bool isFirst = true;
        bool isHorizontal = false;
        bool isVertical = false;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new AffineTransform2D(devices.DeviceContext);
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

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(effect is null)
                return effectDescription.DrawDescription;

            var isHorizontal = flipEffect.IsHorizontal;
            var isVertical = flipEffect.IsVertical;

            if(isFirst || this.isHorizontal != isHorizontal || this.isVertical != isVertical)
            {
                float scaleX = isHorizontal ? -1.0f : 1.0f;
                float scaleY = isVertical ? -1.0f : 1.0f;
                effect.TransformMatrix = Matrix3x2.CreateScale(scaleX, scaleY);

                isFirst = false;
                this.isHorizontal = isHorizontal;
                this.isVertical = isVertical;
            }
            return effectDescription.DrawDescription;
        }
    }
}