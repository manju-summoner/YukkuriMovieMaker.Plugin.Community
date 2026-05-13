using System.Windows.Media;
using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Player;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Tritone;

internal sealed class TritoneEffectProcessor : VideoEffectProcessorBase
{
    private readonly TritoneEffect _item;

    private TritoneCustomEffect? _tritoneEffect;
    private D2DEffects.Composite? _compositeEffect;
    private D2DEffects.Blend? _blendEffect;
    private D2DEffects.CrossFade? _crossFadeEffect;

    private bool _isFirst = true;
    private Color _shadowColor;
    private Color _midtoneColor;
    private Color _highlightColor;
    private double _midPosition;
    private float _opacity;
    private Project.Blend _blendMode;

    public TritoneEffectProcessor(IGraphicsDevicesAndContext devices, TritoneEffect item)
        : base(devices)
    {
        _item = item;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var tritone = new TritoneCustomEffect(devices);
        if (!tritone.IsEnabled)
        {
            tritone.Dispose();
            return null;
        }

        _tritoneEffect = tritone;
        disposer.Collect(_tritoneEffect);

        _compositeEffect = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 2 };
        disposer.Collect(_compositeEffect);
        using (var tritoneOutput = _tritoneEffect.Output)
            _compositeEffect.SetInput(1, tritoneOutput, true);

        _blendEffect = new D2DEffects.Blend(devices.DeviceContext);
        disposer.Collect(_blendEffect);
        using (var tritoneOutput = _tritoneEffect.Output)
            _blendEffect.SetInput(1, tritoneOutput, true);

        _crossFadeEffect = new D2DEffects.CrossFade(devices.DeviceContext);
        disposer.Collect(_crossFadeEffect);

        var output = _crossFadeEffect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _tritoneEffect?.SetInput(0, input, true);
        _compositeEffect?.SetInput(0, input, true);
        _blendEffect?.SetInput(0, input, true);
        _crossFadeEffect?.SetInput(1, input, true);
    }

    protected override void ClearEffectChain()
    {
        _tritoneEffect?.SetInput(0, null, true);
        _compositeEffect?.SetInput(0, null, true);
        _compositeEffect?.SetInput(1, null, true);
        _blendEffect?.SetInput(0, null, true);
        _blendEffect?.SetInput(1, null, true);
        _crossFadeEffect?.SetInput(0, null, true);
        _crossFadeEffect?.SetInput(1, null, true);
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect
            || _tritoneEffect is null
            || _compositeEffect is null
            || _blendEffect is null
            || _crossFadeEffect is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        var shadowColor = _item.ShadowColor;
        var midtoneColor = _item.MidtoneColor;
        var highlightColor = _item.HighlightColor;
        var midPosition = _item.MidPosition.GetValue(frame, length, fps) / 100d;
        var opacity = (float)(_item.Opacity.GetValue(frame, length, fps) / 100d);
        var blendMode = _item.BlendMode;

        if (_isFirst || _shadowColor != shadowColor)
        {
            _tritoneEffect.ShadowR = shadowColor.R / 255f;
            _tritoneEffect.ShadowG = shadowColor.G / 255f;
            _tritoneEffect.ShadowB = shadowColor.B / 255f;
            _shadowColor = shadowColor;
        }

        if (_isFirst || _midtoneColor != midtoneColor)
        {
            _tritoneEffect.MidtoneR = midtoneColor.R / 255f;
            _tritoneEffect.MidtoneG = midtoneColor.G / 255f;
            _tritoneEffect.MidtoneB = midtoneColor.B / 255f;
            _midtoneColor = midtoneColor;
        }

        if (_isFirst || _highlightColor != highlightColor)
        {
            _tritoneEffect.HighlightR = highlightColor.R / 255f;
            _tritoneEffect.HighlightG = highlightColor.G / 255f;
            _tritoneEffect.HighlightB = highlightColor.B / 255f;
            _highlightColor = highlightColor;
        }

        if (_isFirst || _midPosition != midPosition)
        {
            _tritoneEffect.MidPosition = (float)midPosition;
            _midPosition = midPosition;
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
            _blendMode = blendMode;
        }

        if (_isFirst || _opacity != opacity)
        {
            _crossFadeEffect.Weight = opacity;
            _opacity = opacity;
        }

        _isFirst = false;
        return effectDescription.DrawDescription;
    }
}
