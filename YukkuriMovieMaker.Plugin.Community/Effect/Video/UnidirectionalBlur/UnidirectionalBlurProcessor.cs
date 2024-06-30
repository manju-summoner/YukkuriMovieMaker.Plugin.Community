using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.UnidirectionalBlur
{
    internal class UnidirectionalBlurProcessor(IGraphicsDevicesAndContext devices, UnidirectionalBlurEffect unidirectionalBlurEffect) : VideoEffectProcessorBase(devices)
    {
        UnidirectionalBlurCustomEffect? unidirectionalBlurCustomEffect;

        bool isFirst = true;
        double angle, length;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(IsPassThroughEffect || unidirectionalBlurCustomEffect is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var angle = unidirectionalBlurEffect.Angle.GetValue(frame,length,fps);
            var blurLength = unidirectionalBlurEffect.Length.GetValue(frame,length,fps);

            if(isFirst || this.angle != angle)
                unidirectionalBlurCustomEffect.Angle = (float)angle / 180f * (float)Math.PI;
            if(isFirst || this.length != blurLength)
                unidirectionalBlurCustomEffect.Length = (int)Math.Max(1, blurLength + 1);

            isFirst = false;
            this.angle = angle;
            this.length = blurLength;

            return effectDescription.DrawDescription;
        }


        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            unidirectionalBlurCustomEffect = new(devices);
            if(!unidirectionalBlurCustomEffect.IsEnabled)
            {
                unidirectionalBlurCustomEffect.Dispose();
                unidirectionalBlurCustomEffect = null;
                return null;
            }
            disposer.Collect(unidirectionalBlurCustomEffect);

            var output = unidirectionalBlurCustomEffect.Output;
            disposer.Collect(output);

            return output;
        }

        protected override void ClearEffectChain()
        {
            unidirectionalBlurCustomEffect?.SetInput(0, null, true);
        }

        protected override void setInput(ID2D1Image? input)
        {
            unidirectionalBlurCustomEffect?.SetInput(0, input, true);
        }
    }
}