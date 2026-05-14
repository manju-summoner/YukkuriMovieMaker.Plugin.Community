using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using ProjectBlend = YukkuriMovieMaker.Project.Blend;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Player;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal sealed class LutEffectProcessor : VideoEffectProcessorBase
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly LutEffect _item;

    private LutCustomEffect? _lutEffect;
    private D2DEffects.Composite? _compositeEffect;
    private D2DEffects.Blend? _blendEffect;
    private D2DEffects.CrossFade? _crossFadeEffect;
    private ID2D1Bitmap? _lutBitmap;
    private ID2D1Bitmap? _identityBitmap;

    private string _loadedPath = string.Empty;
    private float _opacity;
    private ProjectBlend _blendMode;
    private LutInterpolationMode _interpolation;

    private bool _lutDirty = true;
    private bool _blendModeDirty = true;
    private bool _opacityDirty = true;
    private bool _interpolationDirty = true;

    public LutEffectProcessor(IGraphicsDevicesAndContext devices, LutEffect item)
        : base(devices)
    {
        _devices = devices;
        _item = item;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var lut = new LutCustomEffect(devices);
        if (!lut.IsEnabled)
        {
            lut.Dispose();
            return null;
        }

        var identity = CubeLutTextureFactory.CreateIdentityAtlas(devices.DeviceContext);
        if (identity is null)
        {
            lut.Dispose();
            return null;
        }

        _lutEffect = lut;
        _identityBitmap = identity;
        disposer.Collect(_lutEffect);
        disposer.Collect(_identityBitmap);

        _lutEffect.LutSize = 2f;
        _lutEffect.AtlasWidth = 4f;
        _lutEffect.AtlasHeight = 2f;
        _lutEffect.InterpolationMode = (int)LutInterpolationMode.Tetrahedral;
        _lutEffect.SetDomain(0f, 0f, 0f, 1f, 1f, 1f);
        _lutEffect.SetLutInput(_identityBitmap);

        _compositeEffect = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 2 };
        disposer.Collect(_compositeEffect);
        using (var lutOutput = _lutEffect.Output)
            _compositeEffect.SetInput(1, lutOutput, true);

        _blendEffect = new D2DEffects.Blend(devices.DeviceContext);
        disposer.Collect(_blendEffect);
        using (var lutOutput = _lutEffect.Output)
            _blendEffect.SetInput(1, lutOutput, true);

        _crossFadeEffect = new D2DEffects.CrossFade(devices.DeviceContext);
        disposer.Collect(_crossFadeEffect);

        var output = _crossFadeEffect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _lutEffect?.SetSourceInput(input);
        _compositeEffect?.SetInput(0, input, true);
        _blendEffect?.SetInput(0, input, true);
        _crossFadeEffect?.SetInput(1, input, true);
    }

    protected override void ClearEffectChain()
    {
        _lutEffect?.SetSourceInput(null);
        _lutEffect?.SetLutInput(null);
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
            || _lutEffect is null
            || _compositeEffect is null
            || _blendEffect is null
            || _crossFadeEffect is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        var opacity = (float)(_item.Opacity.GetValue(frame, length, fps) / 100d);
        var blendMode = _item.BlendMode;
        var path = _item.FilePath ?? string.Empty;
        var interpolation = _item.Interpolation;

        var fileChanged = !string.Equals(path, _loadedPath, StringComparison.Ordinal);

        if (_lutDirty || fileChanged)
        {
            RefreshLut(path);
            _lutDirty = false;
        }

        if (_interpolationDirty || _interpolation != interpolation)
        {
            _lutEffect.InterpolationMode = (int)interpolation;
            _interpolation = interpolation;
            _interpolationDirty = false;
        }

        if (_blendModeDirty || _blendMode != blendMode)
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
            _blendModeDirty = false;
        }

        if (_opacityDirty || _opacity != opacity)
        {
            _crossFadeEffect.Weight = opacity;
            _opacity = opacity;
            _opacityDirty = false;
        }

        return effectDescription.DrawDescription;
    }

    private void RefreshLut(string path)
    {
        ReleaseLutBitmap();

        if (string.IsNullOrWhiteSpace(path))
        {
            _loadedPath = path;
            ApplyIdentity();
            return;
        }

        var lut = LutParserRegistry.Parse(path);
        if (lut is null)
        {
            ApplyIdentity();
            return;
        }

        var bitmap = CubeLutTextureFactory.CreateAtlas(_devices.DeviceContext, lut);
        if (bitmap is null)
        {
            ApplyIdentity();
            return;
        }

        _loadedPath = path;
        _lutBitmap = bitmap;
        disposer.Collect(_lutBitmap);

        var n = lut.Size3D;
        _lutEffect!.LutSize = n;
        _lutEffect.AtlasWidth = n * n;
        _lutEffect.AtlasHeight = n;
        _lutEffect.SetDomain(lut);
        _lutEffect.SetLutInput(_lutBitmap);
    }

    private void ApplyIdentity()
    {
        if (_lutEffect is null) return;
        _lutEffect.LutSize = 2f;
        _lutEffect.AtlasWidth = 4f;
        _lutEffect.AtlasHeight = 2f;
        _lutEffect.SetDomain(0f, 0f, 0f, 1f, 1f, 1f);
        _lutEffect.SetLutInput(_identityBitmap);
    }

    private void ReleaseLutBitmap()
    {
        if (_lutBitmap is null) return;
        disposer.RemoveAndDispose(ref _lutBitmap);
    }
}
