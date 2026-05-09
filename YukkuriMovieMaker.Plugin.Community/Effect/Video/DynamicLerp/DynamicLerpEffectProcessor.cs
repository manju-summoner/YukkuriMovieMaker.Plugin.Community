using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DynamicLerp
{
    internal sealed class DynamicLerpEffectProcessor : VideoEffectProcessorBase
    {
        private readonly DynamicLerpEffect _item;

        private DynamicLerpCustomEffect? _lerpEffect;
        private D2DEffects.AffineTransform2D? _sink;

        private bool _isFirst = true;
        private int _weightSource;
        private int _targetIndex;
        private int _mapIndex;

        public DynamicLerpEffectProcessor(IGraphicsDevicesAndContext devices, DynamicLerpEffect item)
            : base(devices)
        {
            _item = item;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            var lerp = new DynamicLerpCustomEffect(devices);
            if (!lerp.IsEnabled)
            {
                lerp.Dispose();
                return null;
            }

            _lerpEffect = lerp;
            disposer.Collect(_lerpEffect);

            _sink = new D2DEffects.AffineTransform2D(devices.DeviceContext);
            disposer.Collect(_sink);

            var output = _sink.Output;
            disposer.Collect(output);
            return output;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            var desc = effectDescription.DrawDescription;

            if (IsPassThroughEffect || _lerpEffect is null || _sink is null || input is null)
                return desc;

            var targetIndex = _item.TargetIndex;
            var mapIndex = _item.MapIndex;
            var weightSource = (int)_item.WeightSource;

            var hasTarget = desc.TryGetCustomValue<ID2D1Image>(out var targetImage, $"OutputBranch.Branch{targetIndex}");
            var hasMap = desc.TryGetCustomValue<ID2D1Image>(out var mapImage, $"OutputBranch.Branch{mapIndex}");

            if (!hasTarget || !hasMap)
            {
                _sink.SetInput(0, input, true);
                return desc;
            }

            if (_isFirst || _weightSource != weightSource)
                _lerpEffect.WeightSource = weightSource;

            if (_isFirst || _targetIndex != targetIndex || _mapIndex != mapIndex || _weightSource != weightSource)
            {
                _lerpEffect.SetCurrentInput(input);
                _lerpEffect.SetTargetInput(targetImage);
                _lerpEffect.SetMapInput(mapImage);
            }

            using var lerpOutput = _lerpEffect.Output;
            _sink.SetInput(0, lerpOutput, true);

            _isFirst = false;
            _weightSource = weightSource;
            _targetIndex = targetIndex;
            _mapIndex = mapIndex;

            return desc;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _lerpEffect?.SetCurrentInput(input);
        }

        protected override void ClearEffectChain()
        {
            _lerpEffect?.SetCurrentInput(null);
            _lerpEffect?.SetTargetInput(null);
            _lerpEffect?.SetMapInput(null);
            _sink?.SetInput(0, null, true);
        }
    }
}
