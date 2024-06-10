using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeDetection
{
    internal class EdgeDetectionEffectProcessor(IGraphicsDevicesAndContext devices, EdgeDetectionEffect item) : VideoEffectProcessorBase(devices)
    {
        Vortice.Direct2D1.Effects.EdgeDetection? effect;

        bool isFirst = true;
        double strength, blurRadius;
        EdgeDetectionMode mode;
        bool isOverlayEdges;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if(effect is null)
                return effectDescription.DrawDescription;

            var fps = effectDescription.FPS;
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;

            var strength = item.Strength.GetValue(frame, length, fps) / 100;
            var blurRadius = item.BlurRadius.GetValue(frame, length, fps);
            var mode = item.Mode;
            var isOverlayEdges = item.IsOverlayEdges;

            if (isFirst || this.strength != strength)
                effect.Strength = (float)strength;

            if (isFirst || this.blurRadius != blurRadius)
                effect.BlurRadius = (float)blurRadius;

            if (isFirst || this.mode != mode)
                effect.Mode = (Vortice.Direct2D1.EdgeDetectionMode)mode;

            if (isFirst || this.isOverlayEdges != isOverlayEdges)
                effect.OverlayEdges = isOverlayEdges;

            isFirst = false;
            this.strength = strength;
            this.blurRadius = blurRadius;
            this.mode = mode;
            this.isOverlayEdges = isOverlayEdges;

            return effectDescription.DrawDescription;
        }


        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new Vortice.Direct2D1.Effects.EdgeDetection(devices.DeviceContext);
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