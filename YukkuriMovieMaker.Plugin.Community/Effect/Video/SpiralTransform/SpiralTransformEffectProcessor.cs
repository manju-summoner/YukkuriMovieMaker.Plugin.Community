using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.SpiralTransform
{
    public class SpiralTransformEffectProcessor(IGraphicsDevicesAndContext devices, SpiralTransformEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly SpiralTransformEffect item = item;

        SpiralTransformCustomEffect? spiralTransform;
        bool isFirst = true, isRotateOuter;
        double angle;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(IsPassThroughEffect || spiralTransform is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var angle = item.Angle.GetValue(frame, length, fps);
            var isRotateOuter = item.IsRotateOuter;

            if (isFirst || this.angle != angle)
                spiralTransform.Angle = (float)(angle / 180 * Math.PI);
            if (isFirst || this.isRotateOuter != isRotateOuter)
                spiralTransform.IsRotateOuter = isRotateOuter;

            isFirst = false;
            this.angle = angle;
            this.isRotateOuter = isRotateOuter;

            return effectDescription.DrawDescription;
        }


        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            spiralTransform = new SpiralTransformCustomEffect(devices);
            if (!spiralTransform.IsEnabled)
            {
                spiralTransform.Dispose();
                spiralTransform = null;
                return null;
            }
            disposer.Collect(spiralTransform);

            var output = spiralTransform.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            spiralTransform?.SetInput(0, input, true);
        }
        protected override void ClearEffectChain()
        {
            spiralTransform?.SetInput(0, null, true);
        }
    }
}