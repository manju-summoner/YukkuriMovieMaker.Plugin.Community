using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeTrimming
{
    internal class EdgeTrimmingEffectProcessor : VideoEffectProcessorBase
    {
        readonly EdgeTrimmingEffect item;
        EdgeTrimmingCustomEffect? effect;

        bool isFirst = true;
        double thickness;

        public EdgeTrimmingEffectProcessor(IGraphicsDevicesAndContext devices, EdgeTrimmingEffect item) : base(devices)
        {
            this.item = item;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var thickness = item.Thickness.GetValue(frame, length, fps);

            if (isFirst || this.thickness != thickness)
                effect.Thickness = (float)thickness;

            isFirst = false;
            this.thickness = thickness;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            effect = new EdgeTrimmingCustomEffect(devices);
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
            effect?.SetInput(0, input, true);
        }
    }
}