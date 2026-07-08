using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Fog
{
    internal sealed class FogEffectProcessor(
        IGraphicsDevicesAndContext devices,
        FogEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly FogEffect _item = item;
        private FogCustomEffect? _effect;

        private bool _isFirst = true;
        private Parameters _parameters;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var density = _item.Density.GetValue(frame, length, fps);
            var scale = _item.Scale.GetValue(frame, length, fps);
            var unevenness = _item.Unevenness.GetValue(frame, length, fps);
            var gradient = _item.Gradient.GetValue(frame, length, fps);
            var flowSpeed = _item.FlowSpeed.GetValue(frame, length, fps);
            var flowAngle = _item.FlowAngle.GetValue(frame, length, fps);
            var changeSpeed = _item.ChangeSpeed.GetValue(frame, length, fps);
            var sunIntensity = _item.SunIntensity.GetValue(frame, length, fps);
            var sunAngle = _item.SunAngle.GetValue(frame, length, fps);
            var fogColor = _item.FogColor;
            var sunColor = _item.SunColor;
            var seed = _item.Seed;

            var flowRad = flowAngle * Math.PI / 180.0;
            var flowU = flowSpeed / 100.0;
            var fogAlpha = fogColor.A / 255f;
            var sunAlpha = sunColor.A / 255f;

            var parameters = new Parameters(
                (float)(density / 100.0) * fogAlpha,
                1f / (1.5f * Math.Max((float)scale, 1f)),
                (float)(unevenness / 100.0),
                (float)(gradient / 100.0),
                (float)(Math.Cos(flowRad) * flowU),
                (float)(Math.Sin(flowRad) * flowU),
                (float)(changeSpeed / 100.0),
                (float)(sunIntensity / 100.0) * sunAlpha,
                (float)(sunAngle * Math.PI / 180.0),
                fogColor.R / 255f,
                fogColor.G / 255f,
                fogColor.B / 255f,
                sunColor.R / 255f,
                sunColor.G / 255f,
                sunColor.B / 255f,
                seed);

            if (_isFirst || _parameters.Density != parameters.Density)
                _effect.Density = parameters.Density;
            if (_isFirst || _parameters.InvFeature != parameters.InvFeature)
                _effect.InvFeature = parameters.InvFeature;
            if (_isFirst || _parameters.Unevenness != parameters.Unevenness)
                _effect.Unevenness = parameters.Unevenness;
            if (_isFirst || _parameters.Gradient != parameters.Gradient)
                _effect.Gradient = parameters.Gradient;
            if (_isFirst || _parameters.FlowX != parameters.FlowX)
                _effect.FlowX = parameters.FlowX;
            if (_isFirst || _parameters.FlowY != parameters.FlowY)
                _effect.FlowY = parameters.FlowY;
            if (_isFirst || _parameters.BoilSpeed != parameters.BoilSpeed)
                _effect.BoilSpeed = parameters.BoilSpeed;
            if (_isFirst || _parameters.SunIntensity != parameters.SunIntensity)
                _effect.SunIntensity = parameters.SunIntensity;
            if (_isFirst || _parameters.SunAngle != parameters.SunAngle)
                _effect.SunAngle = parameters.SunAngle;
            if (_isFirst || _parameters.FogR != parameters.FogR)
                _effect.FogR = parameters.FogR;
            if (_isFirst || _parameters.FogG != parameters.FogG)
                _effect.FogG = parameters.FogG;
            if (_isFirst || _parameters.FogB != parameters.FogB)
                _effect.FogB = parameters.FogB;
            if (_isFirst || _parameters.SunR != parameters.SunR)
                _effect.SunR = parameters.SunR;
            if (_isFirst || _parameters.SunG != parameters.SunG)
                _effect.SunG = parameters.SunG;
            if (_isFirst || _parameters.SunB != parameters.SunB)
                _effect.SunB = parameters.SunB;
            if (_isFirst || _parameters.Seed != parameters.Seed)
                _effect.Seed = parameters.Seed;

            _effect.Time = (float)((double)frame / fps);

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new FogCustomEffect(devices);
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
            float Density,
            float InvFeature,
            float Unevenness,
            float Gradient,
            float FlowX,
            float FlowY,
            float BoilSpeed,
            float SunIntensity,
            float SunAngle,
            float FogR,
            float FogG,
            float FogB,
            float SunR,
            float SunG,
            float SunB,
            int Seed);
    }
}
