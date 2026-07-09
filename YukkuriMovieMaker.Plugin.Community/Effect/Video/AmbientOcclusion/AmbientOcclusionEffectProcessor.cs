using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AmbientOcclusion
{
    internal sealed class AmbientOcclusionEffectProcessor(
        IGraphicsDevicesAndContext devices,
        AmbientOcclusionEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly AmbientOcclusionEffect _item = item;
        private AmbientOcclusionCustomEffect? _effect;

        private bool _isFirst = true;
        private Parameters _parameters;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var shadowColor = _item.ShadowColor;
            var shadowAlpha = shadowColor.A / 255f;

            var parameters = new Parameters(
                (float)(_item.Strength.GetValue(frame, length, fps) / 100.0) * shadowAlpha,
                (float)_item.Radius.GetValue(frame, length, fps),
                (float)(_item.Height.GetValue(frame, length, fps) / 100.0),
                (float)Math.Round(_item.Directions.GetValue(frame, length, fps)),
                (float)Math.Round(_item.Samples.GetValue(frame, length, fps)),
                shadowColor.R / 255f,
                shadowColor.G / 255f,
                shadowColor.B / 255f);

            if (_isFirst || _parameters.Strength != parameters.Strength)
                _effect.Strength = parameters.Strength;
            if (_isFirst || _parameters.Radius != parameters.Radius)
                _effect.Radius = parameters.Radius;
            if (_isFirst || _parameters.HeightGain != parameters.HeightGain)
                _effect.HeightGain = parameters.HeightGain;
            if (_isFirst || _parameters.Directions != parameters.Directions)
                _effect.Directions = parameters.Directions;
            if (_isFirst || _parameters.Samples != parameters.Samples)
                _effect.Samples = parameters.Samples;
            if (_isFirst || _parameters.ShadowR != parameters.ShadowR)
                _effect.ShadowR = parameters.ShadowR;
            if (_isFirst || _parameters.ShadowG != parameters.ShadowG)
                _effect.ShadowG = parameters.ShadowG;
            if (_isFirst || _parameters.ShadowB != parameters.ShadowB)
                _effect.ShadowB = parameters.ShadowB;

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new AmbientOcclusionCustomEffect(devices);
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
            float Strength,
            float Radius,
            float HeightGain,
            float Directions,
            float Samples,
            float ShadowR,
            float ShadowG,
            float ShadowB);
    }
}
