using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Kaleidoscope
{
    internal sealed class KaleidoscopeEffectProcessor(
        IGraphicsDevicesAndContext devices,
        KaleidoscopeEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly KaleidoscopeEffect _item = item;
        private KaleidoscopeCustomEffect? _effect;

        private bool _isFirst = true;
        private Parameters _parameters;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var parameters = new Parameters(
                (float)_item.Segments.GetValue(frame, length, fps),
                (float)(_item.Rotation.GetValue(frame, length, fps) * Math.PI / 180.0),
                (float)(_item.Zoom.GetValue(frame, length, fps) / 100.0),
                (float)(_item.CenterX.GetValue(frame, length, fps) / 100.0),
                (float)(_item.CenterY.GetValue(frame, length, fps) / 100.0),
                _item.Mirror ? 1f : 0f,
                (float)(_item.Amount.GetValue(frame, length, fps) / 100.0));

            if (_isFirst || _parameters.Segments != parameters.Segments)
                _effect.Segments = parameters.Segments;
            if (_isFirst || _parameters.Rotation != parameters.Rotation)
                _effect.Rotation = parameters.Rotation;
            if (_isFirst || _parameters.Zoom != parameters.Zoom)
                _effect.Zoom = parameters.Zoom;
            if (_isFirst || _parameters.CenterX != parameters.CenterX)
                _effect.CenterX = parameters.CenterX;
            if (_isFirst || _parameters.CenterY != parameters.CenterY)
                _effect.CenterY = parameters.CenterY;
            if (_isFirst || _parameters.Mirror != parameters.Mirror)
                _effect.Mirror = parameters.Mirror;
            if (_isFirst || _parameters.Amount != parameters.Amount)
                _effect.Amount = parameters.Amount;

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new KaleidoscopeCustomEffect(devices);
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
            _isFirst = true;
        }

        private readonly record struct Parameters(
            float Segments,
            float Rotation,
            float Zoom,
            float CenterX,
            float CenterY,
            float Mirror,
            float Amount);
    }
}
