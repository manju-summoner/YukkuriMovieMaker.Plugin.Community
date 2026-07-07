using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FacetedGlass
{
    internal sealed class FacetedGlassEffectProcessor(
        IGraphicsDevicesAndContext devices,
        FacetedGlassEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly FacetedGlassEffect _item = item;
        private FacetedGlassCustomEffect? _effect;

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
                (float)(_item.Amount.GetValue(frame, length, fps) / 100.0),
                (float)_item.CellSize.GetValue(frame, length, fps),
                (float)(_item.Irregularity.GetValue(frame, length, fps) / 100.0),
                (float)(_item.Relief.GetValue(frame, length, fps) / 100.0),
                (float)_item.Rotation.GetValue(frame, length, fps),
                (float)(_item.Evolution.GetValue(frame, length, fps) * Math.PI / 180.0),
                (float)_item.Refraction.GetValue(frame, length, fps),
                (float)_item.RefractiveIndex.GetValue(frame, length, fps),
                (float)(_item.Dispersion.GetValue(frame, length, fps) / 100.0),
                (float)(_item.Reflection.GetValue(frame, length, fps) / 100.0),
                (float)(_item.Glint.GetValue(frame, length, fps) / 100.0),
                (float)_item.BorderWidth.GetValue(frame, length, fps),
                (float)_item.LightAngle.GetValue(frame, length, fps),
                (float)_item.LightElevation.GetValue(frame, length, fps),
                _item.Seed);

            if (_isFirst || _parameters.Amount != parameters.Amount)
                _effect.Amount = parameters.Amount;
            if (_isFirst || _parameters.CellSize != parameters.CellSize)
                _effect.CellSize = parameters.CellSize;
            if (_isFirst || _parameters.Irregularity != parameters.Irregularity)
                _effect.Irregularity = parameters.Irregularity;
            if (_isFirst || _parameters.Relief != parameters.Relief)
                _effect.Relief = parameters.Relief;
            if (_isFirst || _parameters.Rotation != parameters.Rotation)
                _effect.Rotation = parameters.Rotation;
            if (_isFirst || _parameters.Evolution != parameters.Evolution)
                _effect.Evolution = parameters.Evolution;
            if (_isFirst || _parameters.Refraction != parameters.Refraction)
                _effect.Refraction = parameters.Refraction;
            if (_isFirst || _parameters.RefractiveIndex != parameters.RefractiveIndex)
                _effect.RefractiveIndex = parameters.RefractiveIndex;
            if (_isFirst || _parameters.Dispersion != parameters.Dispersion)
                _effect.Dispersion = parameters.Dispersion;
            if (_isFirst || _parameters.Reflection != parameters.Reflection)
                _effect.Reflection = parameters.Reflection;
            if (_isFirst || _parameters.Glint != parameters.Glint)
                _effect.Glint = parameters.Glint;
            if (_isFirst || _parameters.BorderWidth != parameters.BorderWidth)
                _effect.BorderWidth = parameters.BorderWidth;
            if (_isFirst || _parameters.LightAngle != parameters.LightAngle)
                _effect.LightAngle = parameters.LightAngle;
            if (_isFirst || _parameters.LightElevation != parameters.LightElevation)
                _effect.LightElevation = parameters.LightElevation;
            if (_isFirst || _parameters.Seed != parameters.Seed)
                _effect.Seed = parameters.Seed;

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new FacetedGlassCustomEffect(devices);
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
            float Amount,
            float CellSize,
            float Irregularity,
            float Relief,
            float Rotation,
            float Evolution,
            float Refraction,
            float RefractiveIndex,
            float Dispersion,
            float Reflection,
            float Glint,
            float BorderWidth,
            float LightAngle,
            float LightElevation,
            int Seed);
    }
}
