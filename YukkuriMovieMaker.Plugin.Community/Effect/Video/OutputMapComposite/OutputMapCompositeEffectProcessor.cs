using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputMapComposite
{
    internal sealed class OutputMapCompositeEffectProcessor : VideoEffectProcessorBase
    {
        private readonly OutputMapCompositeEffect _item;

        private OutputMapCompositeCustomEffect? _lerpEffect;
        private D2DEffects.AffineTransform2D? _sink;

        private bool _isFirst = true;
        private int _mapType;
        private ID2D1Image? _lastTargetImage;
        private ID2D1Image? _lastMapImage;

        public OutputMapCompositeEffectProcessor(IGraphicsDevicesAndContext devices, OutputMapCompositeEffect item)
            : base(devices)
        {
            _item = item;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            var lerp = new OutputMapCompositeCustomEffect(devices);
            if (!lerp.IsEnabled)
            {
                lerp.Dispose();
                return null;
            }

            _lerpEffect = lerp;
            disposer.Collect(_lerpEffect);

            _sink = new D2DEffects.AffineTransform2D(devices.DeviceContext);
            disposer.Collect(_sink);

            using (var lerpOutput = _lerpEffect.Output)
                _sink.SetInput(0, lerpOutput, true);

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
            var mapType = _item.MapType;

            if (!desc.TryGetCustomValue<ID2D1Image>(out var targetImage, $"OutputBranch.Branch{targetIndex}"))
                targetImage = input;

            if (!desc.TryGetCustomValue<ID2D1Image>(out var mapImage, $"OutputBranch.Branch{mapIndex}"))
                mapImage = input;

            var mapTypeInt = (int)mapType;

            if (_isFirst || _mapType != mapTypeInt)
                _lerpEffect.MapType = mapTypeInt;

            if (_isFirst || !ReferenceEquals(_lastTargetImage, targetImage))
                _lerpEffect.SetTargetInput(targetImage);
            if (_isFirst || !ReferenceEquals(_lastMapImage, mapImage))
                _lerpEffect.SetMapInput(mapImage);

            _isFirst = false;
            _mapType = mapTypeInt;
            _lastTargetImage = targetImage;
            _lastMapImage = mapImage;

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
            _lastTargetImage = null;
            _lastMapImage = null;
        }
    }
}
