using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Project;
using Color = System.Windows.Media.Color;
using Blend = YukkuriMovieMaker.Project.Blend;
using YukkuriMovieMaker.Player;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeGlow
{
    internal sealed class EdgeGlowEffectProcessor : VideoEffectProcessorBase
    {
        private const float WideRadiusMultiplier = 4.0f;
        private const float WideLayerOpacity = 0.5f;
        private const float MaxGaussianBlurStandardDeviation = 250.0f;

        private readonly EdgeGlowEffect _item;

        private EdgeGlowCustomEffect? _glowEffect;
        private D2DEffects.GaussianBlur? _coreBlur;
        private D2DEffects.GaussianBlur? _wideBlur;
        private D2DEffects.ColorMatrix? _wideAttenuator;
        private D2DEffects.Composite? _bloomComposite;
        private D2DEffects.Composite? _outputComposite;
        private D2DEffects.Blend? _outputBlend;
        private D2DEffects.CrossFade? _crossFadeEffect;

        private bool _isFirst = true;
        private double _threshold, _softness, _thickness, _intensity, _glowRadius, _opacity;
        private bool _includeAlpha, _enableGlowSpread, _ignoreImageBorder;
        private EdgeGlowColorSource _colorSource;
        private Color _glowColor;
        private Blend _blendMode;

        public EdgeGlowEffectProcessor(IGraphicsDevicesAndContext devices, EdgeGlowEffect item) : base(devices)
        {
            _item = item;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect
                || _glowEffect is null
                || _coreBlur is null
                || _wideBlur is null
                || _wideAttenuator is null
                || _bloomComposite is null
                || _outputComposite is null
                || _outputBlend is null
                || _crossFadeEffect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var threshold = _item.Threshold.GetValue(frame, length, fps);
            var softness = _item.Softness.GetValue(frame, length, fps);
            var thickness = _item.Thickness.GetValue(frame, length, fps);
            var intensity = _item.Intensity.GetValue(frame, length, fps);
            var glowRadius = _item.GlowRadius.GetValue(frame, length, fps);
            var opacity = _item.Opacity.GetValue(frame, length, fps) / 100d;
            var includeAlpha = _item.IncludeAlpha;
            var ignoreImageBorder = _item.IgnoreImageBorder;
            var enableGlowSpread = _item.EnableGlowSpread;
            var colorSource = _item.ColorSource;
            var glowColor = _item.GlowColor;
            var blendMode = _item.BlendMode;

            if (_isFirst || _threshold != threshold)
                _glowEffect.Threshold = (float)threshold;
            if (_isFirst || _softness != softness)
                _glowEffect.Softness = (float)softness;
            if (_isFirst || _thickness != thickness)
                _glowEffect.Thickness = (float)thickness;
            if (_isFirst || _intensity != intensity)
                _glowEffect.Intensity = (float)intensity;
            if (_isFirst || _includeAlpha != includeAlpha)
                _glowEffect.IncludeAlpha = includeAlpha ? 1 : 0;
            if (_isFirst || _ignoreImageBorder != ignoreImageBorder)
                _glowEffect.IgnoreImageBorder = ignoreImageBorder ? 1 : 0;
            if (_isFirst || _colorSource != colorSource)
                _glowEffect.UseSourceColor = colorSource == EdgeGlowColorSource.Source ? 1 : 0;
            if (_isFirst || _glowColor != glowColor)
            {
                _glowEffect.ColorR = glowColor.R / 255f;
                _glowEffect.ColorG = glowColor.G / 255f;
                _glowEffect.ColorB = glowColor.B / 255f;
                _glowEffect.ColorA = glowColor.A / 255f;
            }

            if (_isFirst || _glowRadius != glowRadius)
            {
                var radius = (float)Math.Max(glowRadius, 0d);
                var clampedRadius = Math.Min(radius, MaxGaussianBlurStandardDeviation / WideRadiusMultiplier);
                _coreBlur.StandardDeviation = clampedRadius;
                _wideBlur.StandardDeviation = clampedRadius * WideRadiusMultiplier;
            }

            if (_isFirst || _enableGlowSpread != enableGlowSpread)
            {
                using var glowOutput = _glowEffect.Output;
                if (enableGlowSpread)
                {
                    _coreBlur.SetInput(0, glowOutput, true);
                    _wideBlur.SetInput(0, glowOutput, true);
                    using var bloomed = _bloomComposite.Output;
                    _outputComposite.SetInput(1, bloomed, true);
                    _outputBlend.SetInput(1, bloomed, true);
                }
                else
                {
                    _coreBlur.SetInput(0, null, true);
                    _wideBlur.SetInput(0, null, true);
                    _outputComposite.SetInput(1, glowOutput, true);
                    _outputBlend.SetInput(1, glowOutput, true);
                }
            }

            if (_isFirst || _blendMode != blendMode || _enableGlowSpread != enableGlowSpread)
            {
                if (blendMode.IsCompositionEffect())
                {
                    _outputComposite.Mode = blendMode.ToD2DCompositionMode();
                    using var composited = _outputComposite.Output;
                    _crossFadeEffect.SetInput(0, composited, true);
                }
                else
                {
                    _outputBlend.Mode = blendMode.ToD2DBlendMode();
                    using var blended = _outputBlend.Output;
                    _crossFadeEffect.SetInput(0, blended, true);
                }
            }

            if (_isFirst || _opacity != opacity)
                _crossFadeEffect.Weight = (float)opacity;

            _isFirst = false;
            _threshold = threshold;
            _softness = softness;
            _thickness = thickness;
            _intensity = intensity;
            _glowRadius = glowRadius;
            _opacity = opacity;
            _includeAlpha = includeAlpha;
            _ignoreImageBorder = ignoreImageBorder;
            _enableGlowSpread = enableGlowSpread;
            _colorSource = colorSource;
            _glowColor = glowColor;
            _blendMode = blendMode;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            var glow = new EdgeGlowCustomEffect(devices);
            if (!glow.IsEnabled)
            {
                glow.Dispose();
                return null;
            }
            _glowEffect = glow;
            disposer.Collect(_glowEffect);

            _coreBlur = new D2DEffects.GaussianBlur(devices.DeviceContext)
            {
                BorderMode = BorderMode.Hard,
                Optimization = GaussianBlurOptimization.Quality,
            };
            disposer.Collect(_coreBlur);

            _wideBlur = new D2DEffects.GaussianBlur(devices.DeviceContext)
            {
                BorderMode = BorderMode.Hard,
                Optimization = GaussianBlurOptimization.Quality,
            };
            disposer.Collect(_wideBlur);

            _wideAttenuator = new D2DEffects.ColorMatrix(devices.DeviceContext)
            {
                Matrix = new Matrix5x4()
                {
                    M11 = WideLayerOpacity,
                    M12 = 0,
                    M13 = 0,
                    M14 = 0,
                    M21 = 0,
                    M22 = WideLayerOpacity,
                    M23 = 0,
                    M24 = 0,
                    M31 = 0,
                    M32 = 0,
                    M33 = WideLayerOpacity,
                    M34 = 0,
                    M41 = 0,
                    M42 = 0,
                    M43 = 0,
                    M44 = WideLayerOpacity,
                    M51 = 0,
                    M52 = 0,
                    M53 = 0,
                    M54 = 0,
                },
            };
            disposer.Collect(_wideAttenuator);

            using (var wideOutput = _wideBlur.Output)
                _wideAttenuator.SetInput(0, wideOutput, true);

            _bloomComposite = new D2DEffects.Composite(devices.DeviceContext)
            {
                InputCount = 2,
                Mode = CompositeMode.Plus,
            };
            disposer.Collect(_bloomComposite);
            using (var coreOutput = _coreBlur.Output)
                _bloomComposite.SetInput(0, coreOutput, true);
            using (var wideOutput = _wideAttenuator.Output)
                _bloomComposite.SetInput(1, wideOutput, true);

            _outputComposite = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 2 };
            disposer.Collect(_outputComposite);

            _outputBlend = new D2DEffects.Blend(devices.DeviceContext);
            disposer.Collect(_outputBlend);

            _crossFadeEffect = new D2DEffects.CrossFade(devices.DeviceContext);
            disposer.Collect(_crossFadeEffect);

            var output = _crossFadeEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _glowEffect?.SetInput(0, input, true);
            _outputComposite?.SetInput(0, input, true);
            _outputBlend?.SetInput(0, input, true);
            _crossFadeEffect?.SetInput(1, input, true);
        }

        protected override void ClearEffectChain()
        {
            _glowEffect?.SetInput(0, null, true);
            _coreBlur?.SetInput(0, null, true);
            _wideBlur?.SetInput(0, null, true);
            _wideAttenuator?.SetInput(0, null, true);
            _bloomComposite?.SetInput(0, null, true);
            _bloomComposite?.SetInput(1, null, true);
            _outputComposite?.SetInput(0, null, true);
            _outputComposite?.SetInput(1, null, true);
            _outputBlend?.SetInput(0, null, true);
            _outputBlend?.SetInput(1, null, true);
            _crossFadeEffect?.SetInput(0, null, true);
            _crossFadeEffect?.SetInput(1, null, true);
        }
    }
}
