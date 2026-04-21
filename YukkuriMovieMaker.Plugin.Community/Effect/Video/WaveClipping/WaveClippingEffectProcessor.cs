using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal sealed class WaveClippingEffectProcessor : VideoEffectProcessorBase
    {
        private readonly WaveClippingEffect _item;
        private WaveClippingCustomEffect? _effect;

        public WaveClippingEffectProcessor(
            IGraphicsDevicesAndContext devices,
            WaveClippingEffect item)
            : base(devices)
        {
            _item = item;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            _effect.Amplitude = (float)(_item.Amplitude.GetValue(frame, length, fps) / 100.0);
            _effect.Frequency = (float)_item.Frequency.GetValue(frame, length, fps);
            _effect.Phase = (float)_item.Phase.GetValue(frame, length, fps);
            _effect.EdgePosition = (float)(_item.ClipPosition.GetValue(frame, length, fps) / 100.0);
            _effect.BandWidth = (float)(_item.BandWidth.GetValue(frame, length, fps) / 100.0);
            _effect.Softness = (float)_item.Softness.GetValue(frame, length, fps);
            _effect.Mode = (int)_item.Mode;
            _effect.IsInverted = _item.IsInverted ? 1.0f : 0.0f;
            _effect.Rotation = (float)(-_item.Rotation.GetValue(frame, length, fps) * Math.PI / 180.0);

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
