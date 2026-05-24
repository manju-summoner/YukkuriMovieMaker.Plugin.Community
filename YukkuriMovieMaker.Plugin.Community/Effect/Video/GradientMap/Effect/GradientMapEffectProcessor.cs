using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Brush;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;
using YukkuriMovieMaker.Project;
using GradientStop = YukkuriMovieMaker.Brush.GradientStop;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

internal sealed class GradientMapEffectProcessor : VideoEffectProcessorBase
{
    private static readonly GradientStopComparer StopComparer = new();

    private readonly IGraphicsDevicesAndContext _devices;
    private readonly GradientMapEffect _item;

    private GradientMapCustomEffect? _gradMapEffect;
    private D2DEffects.Composite? _compositeEffect;
    private D2DEffects.Blend? _blendEffect;
    private D2DEffects.CrossFade? _crossFadeEffect;
    private ID2D1Bitmap? _gradientBitmap;
    private ID2D1Bitmap? _fallbackBitmap;

    private string _loadedPath = string.Empty;
    private int _loadedIndex;
    private ImmutableList<GradientStop> _loadedStops = [];
    private float _opacity;
    private Project.Blend _blendMode;
    private int _isHorizontal;

    private bool _isFirst = true;

    public GradientMapEffectProcessor(
        IGraphicsDevicesAndContext devices,
        GradientMapEffect item)
        : base(devices)
    {
        _devices = devices;
        _item = item;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var gradMap = new GradientMapCustomEffect(devices);
        if (!gradMap.IsEnabled)
        {
            gradMap.Dispose();
            return null;
        }

        var fallback = CreateFallbackBitmap(devices);
        if (fallback is null)
        {
            gradMap.Dispose();
            return null;
        }

        _gradMapEffect = gradMap;
        _fallbackBitmap = fallback;
        disposer.Collect(_gradMapEffect);
        disposer.Collect(_fallbackBitmap);
        _gradMapEffect.SetGradientInput(_fallbackBitmap);

        _compositeEffect = new D2DEffects.Composite(devices.DeviceContext) { InputCount = 2 };
        disposer.Collect(_compositeEffect);
        using (var gradMapOutput = _gradMapEffect.Output)
            _compositeEffect.SetInput(1, gradMapOutput, true);

        _blendEffect = new D2DEffects.Blend(devices.DeviceContext);
        disposer.Collect(_blendEffect);
        using (var gradMapOutput = _gradMapEffect.Output)
            _blendEffect.SetInput(1, gradMapOutput, true);

        _crossFadeEffect = new D2DEffects.CrossFade(devices.DeviceContext);
        disposer.Collect(_crossFadeEffect);

        var output = _crossFadeEffect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _gradMapEffect?.SetSourceInput(input);
        _compositeEffect?.SetInput(0, input, true);
        _blendEffect?.SetInput(0, input, true);
        _crossFadeEffect?.SetInput(1, input, true);
    }

    protected override void ClearEffectChain()
    {
        _gradMapEffect?.SetSourceInput(null);
        _gradMapEffect?.SetGradientInput(null);
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
            || _gradMapEffect is null
            || _compositeEffect is null
            || _blendEffect is null
            || _crossFadeEffect is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        var opacity = (float)(_item.Opacity.GetValue(frame, length, fps) / 100d);
        var blendMode = _item.BlendMode;
        var stops = _item.CustomGradientStops ?? [];
        var path = _item.GradientFilePath ?? string.Empty;
        var gradientIndex = _item.GradientIndex;

        // グラデーション源が画像以外（インラインStops / GRD / fallback）の場合は1Dテクスチャとなり、
        // 垂直サンプリングすると中央行ピクセルを繰り返し引くだけになるため水平サンプリングに固定する。
        var isHorizontal = (!ImageFormatParser.IsImageFile(path) || _item.IsHorizontal) ? 1 : 0;

        var fileChanged = !string.Equals(path, _loadedPath, StringComparison.Ordinal)
            || gradientIndex != _loadedIndex;
        var stopsChanged = stops.Count != _loadedStops.Count
            || !stops.SequenceEqual(_loadedStops, StopComparer);

        if (_isFirst || fileChanged || stopsChanged)
            RefreshGradientBitmap(path, gradientIndex, stops);

        if (_isFirst || _isHorizontal != isHorizontal)
            _gradMapEffect.IsHorizontal = isHorizontal;

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

        if (_isFirst || _opacity != opacity)
            _crossFadeEffect.Weight = opacity;

        _isFirst = false;
        _isHorizontal = isHorizontal;
        _blendMode = blendMode;
        _opacity = opacity;

        return effectDescription.DrawDescription;
    }

    private void RefreshGradientBitmap(string path, int gradientIndex, ImmutableList<GradientStop> stops)
    {
        if (!string.IsNullOrWhiteSpace(path))
            RefreshGradientBitmapFromFile(path, gradientIndex, stops);
        else if (stops.Count >= 2)
            RefreshGradientBitmapFromStops(stops, path, gradientIndex);
        else
            ApplyFallback(stops, path, gradientIndex);
    }

    private void RefreshGradientBitmapFromStops(ImmutableList<GradientStop> stops, string path, int gradientIndex)
    {
        ReleaseBitmap();
        _loadedStops = [.. stops.Select(x => x.Clone())];
        _loadedPath = path;
        _loadedIndex = gradientIndex;

        var bitmap = GradientTextureFactory.CreateGradientBitmapFromStops(_devices.DeviceContext, stops);
        if (bitmap is null)
        {
            _gradMapEffect?.SetGradientInput(_fallbackBitmap);
            return;
        }

        _gradientBitmap = bitmap;
        disposer.Collect(_gradientBitmap);
        _gradMapEffect?.SetGradientInput(bitmap);
    }

    private void RefreshGradientBitmapFromFile(string path, int gradientIndex, ImmutableList<GradientStop> stops)
    {
        ReleaseBitmap();
        _loadedPath = path;
        _loadedIndex = gradientIndex;
        _loadedStops = [.. stops.Select(x => x.Clone())];

        var bitmap = GradientTextureFactory.CreateGradientBitmap(_devices.DeviceContext, path, gradientIndex);
        if (bitmap is null)
        {
            _gradMapEffect?.SetGradientInput(_fallbackBitmap);
            return;
        }

        _gradientBitmap = bitmap;
        disposer.Collect(_gradientBitmap);
        _gradMapEffect?.SetGradientInput(bitmap);
    }

    private void ApplyFallback(ImmutableList<GradientStop> stops, string path, int gradientIndex)
    {
        ReleaseBitmap();
        _loadedStops = stops;
        _loadedPath = path;
        _loadedIndex = gradientIndex;
        _gradMapEffect?.SetGradientInput(_fallbackBitmap);
    }

    private void ReleaseBitmap()
    {
        if (_gradientBitmap is null) return;
        _gradMapEffect?.SetGradientInput(_fallbackBitmap);
        disposer.RemoveAndDispose(ref _gradientBitmap);
    }

    private static ID2D1Bitmap? CreateFallbackBitmap(IGraphicsDevicesAndContext devices)
    {
        try
        {
            var stops = new GradientColorStop[]
            {
                new(0f, 0, 0, 0, 255),
                new(1f, 255, 255, 255, 255),
            };
            var pixels = GradientExportService.RasterizeGradient(stops);
            return CreateD2DBitmap(devices, pixels, GradientExportService.GradientResolution);
        }
        catch
        {
            return null;
        }
    }

    private static ID2D1Bitmap CreateD2DBitmap(IGraphicsDevicesAndContext devices, byte[] pixels, int width)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var size = new Vortice.Mathematics.SizeI(width, 1);
            var props = new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(
                    Vortice.DXGI.Format.B8G8R8A8_UNorm,
                    Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f, BitmapOptions.None);
            return ((ID2D1DeviceContext1)devices.DeviceContext).CreateBitmap(
                size, handle.AddrOfPinnedObject(), width * 4, props);
        }
        finally
        {
            handle.Free();
        }
    }
}
