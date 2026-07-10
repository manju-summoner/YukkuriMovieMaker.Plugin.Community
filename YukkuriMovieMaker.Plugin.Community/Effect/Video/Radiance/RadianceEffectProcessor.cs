using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    internal sealed class RadianceEffectProcessor(
        IGraphicsDevicesAndContext devices,
        RadianceEffect item) : VideoEffectProcessorBase(devices)
    {
        const int CascadeCount = RadianceGeometry.LevelCount;
        const int OccCount = RadianceGeometry.OccLevelCount;

        private readonly RadianceEffect _item = item;
        private RadianceEmissionCustomEffect? _emissionEffect;
        private readonly RadianceOccupancyCustomEffect?[] _occupancyEffects = new RadianceOccupancyCustomEffect?[OccCount];
        private readonly RadianceCascadeCustomEffect?[] _cascadeEffects = new RadianceCascadeCustomEffect?[CascadeCount];
        private RadianceCompositeCustomEffect? _compositeEffect;

        private bool _isFirst = true;
        private Parameters _parameters;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _emissionEffect is null || _compositeEffect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var lightColor = _item.LightColor;
            var lightAlpha = lightColor.A / 255f;

            var parameters = new Parameters(
                (float)(_item.Strength.GetValue(frame, length, fps) / 100.0),
                (float)_item.Range.GetValue(frame, length, fps),
                (float)(_item.Diffuse.GetValue(frame, length, fps) / 100.0),
                (float)(_item.Ambient.GetValue(frame, length, fps) / 100.0),
                (float)(_item.Threshold.GetValue(frame, length, fps) / 100.0),
                (float)(_item.EmissionGain.GetValue(frame, length, fps) / 100.0) * lightAlpha,
                (float)(_item.Occlusion.GetValue(frame, length, fps) / 100.0),
                lightColor.R / 255f,
                lightColor.G / 255f,
                lightColor.B / 255f);

            if (_isFirst || _parameters.Strength != parameters.Strength)
                _compositeEffect.Strength = parameters.Strength;
            if (_isFirst || _parameters.Diffuse != parameters.Diffuse)
                _compositeEffect.Diffuse = parameters.Diffuse;
            if (_isFirst || _parameters.Ambient != parameters.Ambient)
                _compositeEffect.Ambient = parameters.Ambient;
            if (_isFirst || _parameters.Threshold != parameters.Threshold)
                _emissionEffect.Threshold = parameters.Threshold;
            if (_isFirst || _parameters.EmissionGain != parameters.EmissionGain)
            {
                for (var i = 0; i < CascadeCount; i++)
                {
                    var cascade = _cascadeEffects[i];
                    if (cascade is not null)
                        cascade.Gain = parameters.EmissionGain;
                }
                for (var i = 0; i < OccCount; i++)
                {
                    var occupancy = _occupancyEffects[i];
                    if (occupancy is not null)
                        occupancy.Gain = parameters.EmissionGain;
                }
            }
            if (_isFirst || _parameters.Occlusion != parameters.Occlusion)
                _emissionEffect.Occlusion = parameters.Occlusion;
            if (_isFirst || _parameters.TintR != parameters.TintR)
                _emissionEffect.TintR = parameters.TintR;
            if (_isFirst || _parameters.TintG != parameters.TintG)
                _emissionEffect.TintG = parameters.TintG;
            if (_isFirst || _parameters.TintB != parameters.TintB)
                _emissionEffect.TintB = parameters.TintB;

            if (_isFirst || _parameters.Range != parameters.Range)
            {
                for (var i = 0; i < CascadeCount; i++)
                {
                    var cascade = _cascadeEffects[i];
                    if (cascade is null)
                        continue;
                    cascade.IntervalStart = parameters.Range * RadianceGeometry.IntervalBounds[i];
                    cascade.IntervalEnd = parameters.Range * RadianceGeometry.IntervalBounds[i + 1];
                    cascade.TotalRange = parameters.Range;
                }
                for (var i = 0; i < OccCount; i++)
                {
                    var occupancy = _occupancyEffects[i];
                    if (occupancy is not null)
                        occupancy.TotalRange = parameters.Range;
                }
                _compositeEffect.RangePx = parameters.Range;
            }

            _parameters = parameters;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _emissionEffect = new RadianceEmissionCustomEffect(devices);
            for (var i = 0; i < OccCount; i++)
                _occupancyEffects[i] = new RadianceOccupancyCustomEffect(devices);
            for (var i = 0; i < CascadeCount; i++)
                _cascadeEffects[i] = new RadianceCascadeCustomEffect(devices);
            _compositeEffect = new RadianceCompositeCustomEffect(devices);

            var allEnabled = _emissionEffect.IsEnabled && _compositeEffect.IsEnabled;
            for (var i = 0; i < OccCount; i++)
                allEnabled &= _occupancyEffects[i]!.IsEnabled;
            for (var i = 0; i < CascadeCount; i++)
                allEnabled &= _cascadeEffects[i]!.IsEnabled;

            if (!allEnabled)
            {
                _emissionEffect.Dispose();
                _emissionEffect = null;
                for (var i = 0; i < OccCount; i++)
                {
                    _occupancyEffects[i]?.Dispose();
                    _occupancyEffects[i] = null;
                }
                for (var i = 0; i < CascadeCount; i++)
                {
                    _cascadeEffects[i]?.Dispose();
                    _cascadeEffects[i] = null;
                }
                _compositeEffect.Dispose();
                _compositeEffect = null;
                return null;
            }

            disposer.Collect(_emissionEffect);
            for (var i = 0; i < OccCount; i++)
                disposer.Collect(_occupancyEffects[i]!);
            for (var i = 0; i < CascadeCount; i++)
                disposer.Collect(_cascadeEffects[i]!);
            disposer.Collect(_compositeEffect);

            for (var i = 0; i < OccCount; i++)
                _occupancyEffects[i]!.BuildLevel = i + 1;
            for (var i = 0; i < CascadeCount; i++)
                _cascadeEffects[i]!.Level = i;
            _cascadeEffects[0]!.Cached = true;

            using (var emissionOutput = _emissionEffect.Output)
            {
                _occupancyEffects[0]!.SetInput(0, emissionOutput, true);
                for (var i = 0; i < OccCount; i++)
                    _occupancyEffects[i]!.SetInput(1, emissionOutput, true);
                for (var i = 0; i < CascadeCount; i++)
                    _cascadeEffects[i]!.SetInput(0, emissionOutput, true);
                _cascadeEffects[CascadeCount - 1]!.SetInput(1, emissionOutput, true);
            }

            for (var i = 1; i < OccCount; i++)
            {
                using var prevOutput = _occupancyEffects[i - 1]!.Output;
                _occupancyEffects[i]!.SetInput(0, prevOutput, true);
            }

            using (var occupancyOutput = _occupancyEffects[OccCount - 1]!.Output)
            {
                for (var i = 0; i < CascadeCount; i++)
                    _cascadeEffects[i]!.SetInput(2, occupancyOutput, true);
            }

            for (var i = CascadeCount - 2; i >= 0; i--)
            {
                using var upperOutput = _cascadeEffects[i + 1]!.Output;
                _cascadeEffects[i]!.SetInput(1, upperOutput, true);
            }

            using (var cascadeOutput = _cascadeEffects[0]!.Output)
                _compositeEffect.SetInput(1, cascadeOutput, true);

            var output = _compositeEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _emissionEffect?.SetInput(0, input, true);
            _compositeEffect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            _emissionEffect?.SetInput(0, null, true);
            for (var i = 0; i < OccCount; i++)
            {
                _occupancyEffects[i]?.SetInput(0, null, true);
                _occupancyEffects[i]?.SetInput(1, null, true);
            }
            for (var i = 0; i < CascadeCount; i++)
            {
                _cascadeEffects[i]?.SetInput(0, null, true);
                _cascadeEffects[i]?.SetInput(1, null, true);
                _cascadeEffects[i]?.SetInput(2, null, true);
            }
            _compositeEffect?.SetInput(0, null, true);
            _compositeEffect?.SetInput(1, null, true);
            _isFirst = true;
        }

        private readonly record struct Parameters(
            float Strength,
            float Range,
            float Diffuse,
            float Ambient,
            float Threshold,
            float EmissionGain,
            float Occlusion,
            float TintR,
            float TintG,
            float TintB);
    }
}
