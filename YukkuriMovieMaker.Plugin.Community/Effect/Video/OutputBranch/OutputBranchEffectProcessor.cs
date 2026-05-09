using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputBranch
{
    internal class OutputBranchEffectProcessor : VideoEffectProcessorBase
    {
        private AffineTransform2D? transformEffect;

        public OutputBranchEffectProcessor(IGraphicsDevicesAndContext devices) : base(devices)
        {

        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null || transformEffect is null)
                return desc;

            var next = desc.GetCustomValue<int>("OutputBranch.NextBranchIndex");
            if (next <= 0)
                next = 1;

            desc = desc.SetCustomValue<ID2D1Image>(transformEffect.Output, $"OutputBranch.Branch{next}");
            desc = desc.SetCustomValue<int>(next + 1, "OutputBranch.NextBranchIndex");
            return desc;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            transformEffect = new AffineTransform2D(devices.DeviceContext)
            {
                Cached = true
            };
            return transformEffect.Output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            if (transformEffect != null)
            {
                transformEffect.SetInput(0, input, true);
            }
        }

        protected override void ClearEffectChain()
        {
            if (transformEffect != null)
            {
                transformEffect.Dispose();
                transformEffect = null;
            }
        }
    }
}
