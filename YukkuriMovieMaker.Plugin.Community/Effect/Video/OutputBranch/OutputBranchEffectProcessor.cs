using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputBranch
{
    internal class OutputBranchEffectProcessor(IGraphicsDevicesAndContext devices) : VideoEffectProcessorBase(devices)
    {
        private AffineTransform2D? transformEffect;
        private ID2D1Image? transformOutput;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null || transformOutput is null)
                return desc;

            var next = desc.GetCustomValue<int>("OutputBranch.NextBranchIndex");
            if (next <= 0)
                next = 1;

            desc = desc.SetCustomValue<ID2D1Image>(transformOutput, $"OutputBranch.Branch{next}");
            desc = desc.SetCustomValue<int>(next + 1, "OutputBranch.NextBranchIndex");
            return desc;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            transformEffect = new AffineTransform2D(devices.DeviceContext)
            {
                Cached = true
            };
            disposer.Collect(transformEffect);

            transformOutput = transformEffect.Output;
            disposer.Collect(transformOutput);
            return transformOutput;
        }

        protected override void setInput(ID2D1Image? input)
        {
            transformEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            transformEffect?.SetInput(0, null, true);
        }
    }
}
