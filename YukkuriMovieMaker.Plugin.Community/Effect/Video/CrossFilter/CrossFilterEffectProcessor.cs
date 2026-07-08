using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossFilter
{
    internal sealed class CrossFilterEffectProcessor(
        IGraphicsDevicesAndContext devices,
        CrossFilterEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly CrossFilterEffect _item = item;
        private CrossFilterCustomEffect? _effect;

        private bool _isFirst = true;
        private Parameters _parameters;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var lightColor = _item.LightColor;

            var parameters = new Parameters(
                (float)(_item.Strength.GetValue(frame, length, fps) / 100.0),
                (float)(_item.Threshold.GetValue(frame, length, fps) / 100.0),
                (float)_item.Length.GetValue(frame, length, fps),
                (float)Math.Round(_item.RayCount.GetValue(frame, length, fps)),
                (float)(_item.Angle.GetValue(frame, length, fps) * Math.PI / 180.0),
                (float)(_item.Dispersion.GetValue(frame, length, fps) / 100.0),
                (float)_item.Thickness.GetValue(frame, length, fps),
                lightColor.R / 255f,
                lightColor.G / 255f,
                lightColor.B / 255f,
                _item.LightOnly ? 1 : 0);

            if (_isFirst || _parameters.Strength != parameters.Strength)
                _effect.Strength = parameters.Strength;
            if (_isFirst || _parameters.Threshold != parameters.Threshold)
                _effect.Threshold = parameters.Threshold;
            if (_isFirst || _parameters.Length != parameters.Length)
                _effect.Length = parameters.Length;
            if (_isFirst || _parameters.RayCount != parameters.RayCount)
                _effect.RayCount = parameters.RayCount;
            if (_isFirst || _parameters.Angle != parameters.Angle)
                _effect.Angle = parameters.Angle;
            if (_isFirst || _parameters.Dispersion != parameters.Dispersion)
                _effect.Dispersion = parameters.Dispersion;
            if (_isFirst || _parameters.Thickness != parameters.Thickness)
                _effect.Thickness = parameters.Thickness;
            if (_isFirst || _parameters.LightR != parameters.LightR)
                _effect.LightR = parameters.LightR;
            if (_isFirst || _parameters.LightG != parameters.LightG)
                _effect.LightG = parameters.LightG;
            if (_isFirst || _parameters.LightB != parameters.LightB)
                _effect.LightB = parameters.LightB;
            if (_isFirst || _parameters.LightOnly != parameters.LightOnly)
                _effect.LightOnly = parameters.LightOnly;

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new CrossFilterCustomEffect(devices);
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
            float Threshold,
            float Length,
            float RayCount,
            float Angle,
            float Dispersion,
            float Thickness,
            float LightR,
            float LightG,
            float LightB,
            int LightOnly);
    }
}
