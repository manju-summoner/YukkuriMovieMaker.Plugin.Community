using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputBranch
{
    internal class OutputBranchEffectProcessor(IGraphicsDevicesAndContext devices) : VideoEffectProcessorBase(devices)
    {
        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null)
                return desc;

            var next = desc.GetCustomValue<int>("OutputBranch.NextBranchIndex");
            if (next <= 0)
                next = 1;

            desc = desc.SetCustomValue<ID2D1Image>(input, $"OutputBranch.Branch{next}");
            desc = desc.SetCustomValue<int>(next + 1, "OutputBranch.NextBranchIndex");
            return desc;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices) => null;

        protected override void setInput(ID2D1Image? input) { }

        protected override void ClearEffectChain() { }
    }
}
