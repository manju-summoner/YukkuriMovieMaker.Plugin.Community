using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputSwitch
{
    internal class OutputSwitchEffectProcessor(IGraphicsDevicesAndContext devices, OutputSwitchEffect item) : VideoEffectProcessorBase(devices)
    {
        readonly OutputSwitchEffect item = item;

        D2DEffects.AffineTransform2D? sink;

        bool isFirst = true;
        ID2D1Image? lastSinkInput;

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            sink = new D2DEffects.AffineTransform2D(devices.DeviceContext)
            {
                Cached = true
            };
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

            ID2D1Image? sinkInput;
            if (target == cur)
            {
                sinkInput = input;
            }
            else if (!desc.TryGetCustomValue<ID2D1Image>(out var targetImage, $"OutputBranch.Branch{target}"))
            {
                sinkInput = input;
            }
            else
            {
                desc = desc.SetCustomValue<ID2D1Image>(input, $"OutputBranch.Branch{cur}");
                desc = desc.SetCustomValue<int>(target, "OutputBranch.CurrentIndex");
                sinkInput = targetImage;
            }

            if (isFirst || !ReferenceEquals(lastSinkInput, sinkInput))
                sink.SetInput(0, sinkInput, true);

            isFirst = false;
            lastSinkInput = sinkInput;

            return desc;
        }

        protected override void setInput(ID2D1Image? input)
        {
            sink?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            sink?.SetInput(0, null, true);
            lastSinkInput = null;
        }
    }
}
