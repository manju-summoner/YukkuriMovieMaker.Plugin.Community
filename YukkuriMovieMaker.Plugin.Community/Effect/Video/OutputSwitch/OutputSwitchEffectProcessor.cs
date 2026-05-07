using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputSwitch
{
    internal class OutputSwitchEffectProcessor : VideoEffectProcessorBase
    {
        readonly OutputSwitchEffect item;

        D2DEffects.AffineTransform2D? sink;

        public OutputSwitchEffectProcessor(IGraphicsDevicesAndContext devices, OutputSwitchEffect item) : base(devices)
        {
            this.item = item;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            sink = new D2DEffects.AffineTransform2D(devices.DeviceContext);
            disposer.Collect(sink);

            var output = sink.Output;
            disposer.Collect(output);
            return output;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null || sink is null)
                return desc;

            var cur = desc.GetCustomValue<int>("OutputBranch.CurrentIndex");
            var target = item.TargetIndex;

            if (target == cur)
            {
                sink.SetInput(0, input, true);
                return desc;
            }

            if (!desc.TryGetCustomValue<ID2D1Image>(out var targetImage, $"OutputBranch.Branch{target}"))
            {
                sink.SetInput(0, input, true);
                return desc;
            }

            desc = desc.SetCustomValue<ID2D1Image>(input, $"OutputBranch.Branch{cur}");
            desc = desc.SetCustomValue<int>(target, "OutputBranch.CurrentIndex");
            sink.SetInput(0, targetImage, true);
            return desc;
        }

        protected override void setInput(ID2D1Image? input)
        {
            sink?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            sink?.SetInput(0, null, true);
        }
    }
}
