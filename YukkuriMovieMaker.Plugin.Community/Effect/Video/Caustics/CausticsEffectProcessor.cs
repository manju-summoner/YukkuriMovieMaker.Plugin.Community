using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Caustics
{
    internal sealed class CausticsEffectProcessor(
        IGraphicsDevicesAndContext devices,
        CausticsEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly CausticsEffect _item = item;
        private CausticsCustomEffect? _effect;

        private bool _isFirst = true;
        private Parameters _parameters;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var strength = _item.Strength.GetValue(frame, length, fps);
            var displacement = _item.Displacement.GetValue(frame, length, fps);
            var scale = _item.Scale.GetValue(frame, length, fps);
            var speed = _item.Speed.GetValue(frame, length, fps);
            var sharpness = _item.Sharpness.GetValue(frame, length, fps);
            var focus = _item.Focus.GetValue(frame, length, fps);
            var dispersion = _item.Dispersion.GetValue(frame, length, fps);
            var absorption = _item.Absorption.GetValue(frame, length, fps);
            var flowSpeed = _item.FlowSpeed.GetValue(frame, length, fps);
            var flowAngle = _item.FlowAngle.GetValue(frame, length, fps);
            var anisotropy = _item.Anisotropy.GetValue(frame, length, fps);
            var waveAngle = _item.WaveAngle.GetValue(frame, length, fps);
            var lightColor = _item.LightColor;
            var absorptionColor = _item.AbsorptionColor;
            var lightOnly = _item.LightOnly;
            var seed = _item.Seed;

            var flowRad = flowAngle * Math.PI / 180.0;
            var flowU = flowSpeed / 100.0;

            var parameters = new Parameters(
                (float)(strength / 100.0),
                (float)displacement,
                1f / (1.5f * Math.Max((float)scale, 1f)),
                (float)Math.Pow(10.0, -2.2 * Math.Clamp(sharpness, 0.0, 100.0) / 100.0),
                (float)(dispersion / 100.0 * 0.5),
                (float)(focus / 100.0),
                (float)(absorption / 100.0),
                (float)(Math.Cos(flowRad) * flowU),
                (float)(Math.Sin(flowRad) * flowU),
                1f - (float)(anisotropy / 100.0),
                (float)(waveAngle * Math.PI / 180.0),
                (float)(speed / 100.0),
                lightColor.R / 255f,
                lightColor.G / 255f,
                lightColor.B / 255f,
                absorptionColor.R / 255f,
                absorptionColor.G / 255f,
                absorptionColor.B / 255f,
                lightOnly ? 1 : 0,
                seed);

            if (_isFirst || _parameters.Strength != parameters.Strength)
                _effect.Strength = parameters.Strength;
            if (_isFirst || _parameters.Displacement != parameters.Displacement)
                _effect.Displacement = parameters.Displacement;
            if (_isFirst || _parameters.InvFeature != parameters.InvFeature)
                _effect.InvFeature = parameters.InvFeature;
            if (_isFirst || _parameters.Sigma != parameters.Sigma)
                _effect.Sigma = parameters.Sigma;
            if (_isFirst || _parameters.Dispersion != parameters.Dispersion)
                _effect.Dispersion = parameters.Dispersion;
            if (_isFirst || _parameters.Focus != parameters.Focus)
                _effect.Focus = parameters.Focus;
            if (_isFirst || _parameters.Absorption != parameters.Absorption)
                _effect.Absorption = parameters.Absorption;
            if (_isFirst || _parameters.FlowX != parameters.FlowX)
                _effect.FlowX = parameters.FlowX;
            if (_isFirst || _parameters.FlowY != parameters.FlowY)
                _effect.FlowY = parameters.FlowY;
            if (_isFirst || _parameters.AnisoScale != parameters.AnisoScale)
                _effect.AnisoScale = parameters.AnisoScale;
            if (_isFirst || _parameters.AnisoAngle != parameters.AnisoAngle)
                _effect.AnisoAngle = parameters.AnisoAngle;
            if (_isFirst || _parameters.BoilSpeed != parameters.BoilSpeed)
                _effect.BoilSpeed = parameters.BoilSpeed;
            if (_isFirst || _parameters.LightR != parameters.LightR)
                _effect.LightR = parameters.LightR;
            if (_isFirst || _parameters.LightG != parameters.LightG)
                _effect.LightG = parameters.LightG;
            if (_isFirst || _parameters.LightB != parameters.LightB)
                _effect.LightB = parameters.LightB;
            if (_isFirst || _parameters.AbsorbR != parameters.AbsorbR)
                _effect.AbsorbR = parameters.AbsorbR;
            if (_isFirst || _parameters.AbsorbG != parameters.AbsorbG)
                _effect.AbsorbG = parameters.AbsorbG;
            if (_isFirst || _parameters.AbsorbB != parameters.AbsorbB)
                _effect.AbsorbB = parameters.AbsorbB;
            if (_isFirst || _parameters.LightOnly != parameters.LightOnly)
                _effect.LightOnly = parameters.LightOnly;
            if (_isFirst || _parameters.Seed != parameters.Seed)
                _effect.Seed = parameters.Seed;

            _effect.Time = (float)((double)frame / fps);

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _effect = new CausticsCustomEffect(devices);
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
            float Displacement,
            float InvFeature,
            float Sigma,
            float Dispersion,
            float Focus,
            float Absorption,
            float FlowX,
            float FlowY,
            float AnisoScale,
            float AnisoAngle,
            float BoilSpeed,
            float LightR,
            float LightG,
            float LightB,
            float AbsorbR,
            float AbsorbG,
            float AbsorbB,
            int LightOnly,
            int Seed);
    }
}
