using System.Windows.Media;
using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Dithering
{
    internal sealed class DitheringEffectProcessor(
        IGraphicsDevicesAndContext devices,
        DitheringEffect item) : VideoEffectProcessorBase(devices)
    {
        private readonly DitheringEffect _item = item;

        private DitheringCustomEffect? _dithering;
        private D2DEffects.Composite? _compositeEffect;
        private D2DEffects.Blend? _blendEffect;
        private D2DEffects.CrossFade? _crossFadeEffect;

        private bool _isFirst = true;
        private double _levels;
        private double _scale;
        private double _strength;
        private DitheringMode _mode;
        private Color _darkColor;
        private Color _lightColor;
        private Project.Blend _blendMode;
        private float _amount;

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect
                || _dithering is null
                || _compositeEffect is null
                || _blendEffect is null
                || _crossFadeEffect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            var levels = _item.Levels.GetValue(frame, length, fps);
            var scale = _item.Scale.GetValue(frame, length, fps);
            var strength = _item.Strength.GetValue(frame, length, fps) / 100.0;
            var mode = _item.Mode;
            var darkColor = _item.DarkColor;
            var lightColor = _item.LightColor;
            var blendMode = _item.BlendMode;
            var amount = (float)(_item.Amount.GetValue(frame, length, fps) / 100.0);

            if (_isFirst || _levels != levels)
                _dithering.Levels = (float)levels;
            if (_isFirst || _scale != scale)
                _dithering.Scale = (float)scale;
            if (_isFirst || _strength != strength)
                _dithering.Strength = (float)strength;
            if (_isFirst || _mode != mode)
                _dithering.Mode = (int)mode;
            if (_isFirst || _darkColor != darkColor)
            {
                _dithering.DarkR = darkColor.R / 255f;
                _dithering.DarkG = darkColor.G / 255f;
                _dithering.DarkB = darkColor.B / 255f;
            }
            if (_isFirst || _lightColor != lightColor)
            {
                _dithering.LightR = lightColor.R / 255f;
                _dithering.LightG = lightColor.G / 255f;
                _dithering.LightB = lightColor.B / 255f;
            }
            if (_isFirst || _blendMode != blendMode)
            {
                if (blendMode.IsCompositionEffect())
                {
                    _compositeEffect.Mode = blendMode.ToD2DCompositionMode();
                    using var composited = _compositeEffect.Output;
                    _crossFadeEffect.SetInput(0, composited, true);
                }
                else
                {
                    _blendEffect.Mode = blendMode.ToD2DBlendMode();
                    using var blended = _blendEffect.Output;
                    _crossFadeEffect.SetInput(0, blended, true);
                }
            }
            if (_isFirst || _amount != amount)
                _crossFadeEffect.Weight = amount;

            _levels = levels;
            _scale = scale;
            _strength = strength;
            _mode = mode;
            _darkColor = darkColor;
            _lightColor = lightColor;
            _blendMode = blendMode;
            _amount = amount;
            _isFirst = false;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _dithering = new DitheringCustomEffect(devices);
            if (!_dithering.IsEnabled)
            {
                _dithering.Dispose();
                _dithering = null;
                return null;
            }
            disposer.Collect(_dithering);

            _compositeEffect = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 2 };
            disposer.Collect(_compositeEffect);
            using (var ditheringOutput = _dithering.Output)
                _compositeEffect.SetInput(1, ditheringOutput, true);

            _blendEffect = new D2DEffects.Blend(devices.DeviceContext);
            disposer.Collect(_blendEffect);
            using (var ditheringOutput = _dithering.Output)
                _blendEffect.SetInput(1, ditheringOutput, true);

            _crossFadeEffect = new D2DEffects.CrossFade(devices.DeviceContext);
            disposer.Collect(_crossFadeEffect);

            var output = _crossFadeEffect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _dithering?.SetInput(0, input, true);
            _compositeEffect?.SetInput(0, input, true);
            _blendEffect?.SetInput(0, input, true);
            _crossFadeEffect?.SetInput(1, input, true);
        }

        protected override void ClearEffectChain()
        {
            _dithering?.SetInput(0, null, true);
            _compositeEffect?.SetInput(0, null, true);
            _compositeEffect?.SetInput(1, null, true);
            _blendEffect?.SetInput(0, null, true);
            _blendEffect?.SetInput(1, null, true);
            _crossFadeEffect?.SetInput(0, null, true);
            _crossFadeEffect?.SetInput(1, null, true);
            _isFirst = true;
        }
    }
}
