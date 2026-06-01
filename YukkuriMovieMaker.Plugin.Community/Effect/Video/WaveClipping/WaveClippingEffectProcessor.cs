using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal sealed class WaveClippingEffectProcessor(
        IGraphicsDevicesAndContext devices,
        WaveClippingEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly WaveClippingEffect _item = item;
        private WaveClippingCustomEffect? _effect;

        private bool _isFirst = true;
        private double _amplitude;
        private double _frequency;
        private double _phase;
        private double _clipPosition;
        private double _bandWidth;
        private double _softness;
        private WaveClippingMode _mode;
        private bool _isInverted;
        private double _rotation;
        private int _randomSeed;
        private bool _useRandom;

        private static float NormalizeInt32ToUnitFloat(int value)
            => (uint)value / (float)(uint.MaxValue + 1UL);

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var amplitude = _item.Amplitude.GetValue(frame, length, fps) / 100.0;
            var frequency = _item.Frequency.GetValue(frame, length, fps);
            var phase = _item.Phase.GetValue(frame, length, fps);
            var clipPosition = _item.ClipPosition.GetValue(frame, length, fps) / 100.0;
            var bandWidth = _item.BandWidth.GetValue(frame, length, fps) / 100.0;
            var softness = _item.Softness.GetValue(frame, length, fps);
            var mode = _item.Mode;
            var isInverted = _item.IsInverted;
            var rotation = -_item.Rotation.GetValue(frame, length, fps) * Math.PI / 180.0;
            var randomSeed = _item.GetHashCode();
            var useRandom = _item.UseRandom;

            if (_isFirst || _amplitude != amplitude)
                _effect.Amplitude = (float)amplitude;
            if (_isFirst || _frequency != frequency)
                _effect.Frequency = (float)frequency;
            if (_isFirst || _phase != phase)
                _effect.Phase = (float)phase;
            if (_isFirst || _clipPosition != clipPosition)
                _effect.EdgePosition = (float)clipPosition;
            if (_isFirst || _bandWidth != bandWidth)
                _effect.BandWidth = (float)bandWidth;
            if (_isFirst || _softness != softness)
                _effect.Softness = (float)softness;
            if (_isFirst || _mode != mode)
                _effect.Mode = (int)mode;
            if (_isFirst || _isInverted != isInverted)
                _effect.IsInverted = isInverted ? 1.0f : 0.0f;
            if (_isFirst || _rotation != rotation)
                _effect.Rotation = (float)rotation;
            if (_isFirst || _randomSeed != randomSeed)
                _effect.RandomSeed = NormalizeInt32ToUnitFloat(randomSeed);
            if (_isFirst || _useRandom != useRandom)
                _effect.UseRandom = useRandom ? 1.0f : 0.0f;

            _isFirst = false;
            _amplitude = amplitude;
            _frequency = frequency;
            _phase = phase;
            _clipPosition = clipPosition;
            _bandWidth = bandWidth;
            _softness = softness;
            _mode = mode;
            _isInverted = isInverted;
            _rotation = rotation;
            _randomSeed = randomSeed;
            _useRandom = useRandom;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new WaveClippingCustomEffect(devices);
            if (!_effect.IsEnabled)
            {
                _effect.Dispose();
                _effect = null;
                return null;
            }
            disposer.Collect(_effect);

            var output = _effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            _effect?.SetInput(0, null, true);
        }
    }
}
