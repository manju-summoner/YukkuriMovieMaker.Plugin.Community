using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputComposite
{
    internal class OutputCompositeEffectProcessor : VideoEffectProcessorBase
    {
        readonly OutputCompositeEffect item;
        readonly IGraphicsDevicesAndContext devices;

        D2DEffects.Composite? compositeEffect;
        D2DEffects.Blend? blendEffect;
        D2DEffects.Opacity? opacityEffect;
        D2DEffects.AffineTransform2D? sink;

        public OutputCompositeEffectProcessor(IGraphicsDevicesAndContext devices, OutputCompositeEffect item) : base(devices)
        {
            this.item = item;
            this.devices = devices;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            sink = new D2DEffects.AffineTransform2D(devices.DeviceContext)
            {
                Cached = true
            };
            disposer.Collect(sink);

            compositeEffect = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 2 };
            disposer.Collect(compositeEffect);

            blendEffect = new D2DEffects.Blend(devices.DeviceContext);
            disposer.Collect(blendEffect);

            opacityEffect = new D2DEffects.Opacity(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            using (var opacityOutput = opacityEffect.Output)
            {
                compositeEffect.SetInput(1, opacityOutput, true);
                blendEffect.SetInput(1, opacityOutput, true);
            }

            var output = sink.Output;
            disposer.Collect(output);
            return output;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;
            if (input is null || sink is null || compositeEffect is null || blendEffect is null || opacityEffect is null)
                return desc;

            var cur = desc.GetCustomValue<int>("OutputBranch.CurrentIndex");
            var target = item.TargetIndex;
            var blend = item.BlendMode;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var opacity = item.Opacity.GetValue(frame, length, fps);

            ID2D1Image? src;
            if (target == cur)
            {
                src = input;
            }
            else if (!desc.TryGetCustomValue<ID2D1Image>(out var cvImg, $"OutputBranch.Branch{target}"))
            {
                sink.SetInput(0, input, true);
                return desc;
            }
            else
            {
                src = cvImg;
            }

            opacityEffect.Value = (float)opacity / 100f;
            opacityEffect.SetInput(0, src, true);

            if (blend.IsCompositionEffect())
            {
                compositeEffect.Mode = blend.ToD2DCompositionMode();
                using var output = compositeEffect.Output;
                sink.SetInput(0, output, true);
            }
            else
            {
                blendEffect.Mode = blend.ToD2DBlendMode();
                using var output = blendEffect.Output;
                sink.SetInput(0, output, true);
            }

            return desc;
        }

        protected override void setInput(ID2D1Image? input)
        {
            compositeEffect?.SetInput(0, input, true);
            blendEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            compositeEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(1, null, true);
            blendEffect?.SetInput(0, null, true);
            blendEffect?.SetInput(1, null, true);
            opacityEffect?.SetInput(0, null, true);
            sink?.SetInput(0, null, true);
        }
    }
}
