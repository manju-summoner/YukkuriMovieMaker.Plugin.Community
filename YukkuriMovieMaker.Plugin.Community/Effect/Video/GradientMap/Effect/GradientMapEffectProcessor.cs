using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Brush;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;
using GradientStop = YukkuriMovieMaker.Brush.GradientStop;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

internal sealed class GradientMapEffectProcessor : VideoEffectProcessorBase
{
    private static readonly GradientStopComparer StopComparer = new();

    private readonly IGraphicsDevicesAndContext _devices;
    private readonly GradientMapEffect _item;

    private GradientMapCustomEffect? _effect;
    private ID2D1Bitmap? _gradientBitmap;
    private ID2D1Bitmap? _fallbackBitmap;

    private string _loadedPath = string.Empty;
    private int _loadedIndex = -1;
    private ImmutableList<GradientStop> _loadedStops = [];
    private bool _isFirst = true;
    private float _opacity;
    private int _blendMode;
    private int _isHorizontal;

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
        var effect = new GradientMapCustomEffect(devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return null;
        }

        var fallback = CreateFallbackBitmap(devices);
        if (fallback is null)
        {
            effect.Dispose();
            return null;
        }

        _effect = effect;
        _fallbackBitmap = fallback;
        disposer.Collect(_effect);
        disposer.Collect(_fallbackBitmap);
        _effect.SetGradientInput(_fallbackBitmap);

        var output = _effect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _effect?.SetSourceInput(input);
    }

    protected override void ClearEffectChain()
    {
        _effect?.SetSourceInput(null);
        _effect?.SetGradientInput(null);
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _effect is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        var opacity = (float)(_item.Opacity.GetValue(frame, length, fps) / 100d);
        var blendMode = (int)_item.BlendMode;
        var stops = _item.CustomGradientStops ?? [];
        var path = _item.GradientFilePath ?? string.Empty;
        var gradientIndex = _item.GradientIndex;

        // グラデーション源が画像以外（インラインStops / GRD / fallback）の場合は1Dテクスチャとなり、
        // 垂直サンプリングすると中央行ピクセルを繰り返し引くだけになるため水平サンプリングに固定する。
        var isHorizontal = (!ImageFormatParser.IsImageFile(path) || _item.IsHorizontal) ? 1 : 0;

        var stopsChanged = stops.Count != _loadedStops.Count || !stops.SequenceEqual(_loadedStops, StopComparer);
        var fileChanged = !string.Equals(path, _loadedPath, StringComparison.Ordinal) || gradientIndex != _loadedIndex;
        var hasStops = stops.Count >= 2;

        if (fileChanged && !stopsChanged)
        {
            RefreshGradientBitmapFromFile(path, gradientIndex, stops);
        }
        else if (stopsChanged && !fileChanged)
        {
            if (hasStops)
                RefreshGradientBitmapFromStops(stops, path, gradientIndex);
            else
                RefreshGradientBitmapFromFile(path, gradientIndex, stops);
        }
        else if (stopsChanged && fileChanged)
        {
            if (hasStops)
                RefreshGradientBitmapFromStops(stops, path, gradientIndex);
            else if (!string.IsNullOrWhiteSpace(path))
                RefreshGradientBitmapFromFile(path, gradientIndex, stops);
        }

        if (_isFirst || _opacity != opacity)
            _effect.Opacity = opacity;

        if (_isFirst || _blendMode != blendMode)
            _effect.BlendMode = blendMode;

        if (_isFirst || _isHorizontal != isHorizontal)
            _effect.IsHorizontal = isHorizontal;

        _isFirst = false;
        _opacity = opacity;
        _blendMode = blendMode;
        _isHorizontal = isHorizontal;

        return effectDescription.DrawDescription;
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
            _effect?.SetGradientInput(_fallbackBitmap);
            return;
        }

        _gradientBitmap = bitmap;
        disposer.Collect(_gradientBitmap);
        _effect?.SetGradientInput(bitmap);
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
            _effect?.SetGradientInput(_fallbackBitmap);
            return;
        }

        _gradientBitmap = bitmap;
        disposer.Collect(_gradientBitmap);
        _effect?.SetGradientInput(bitmap);
    }

    private void ReleaseBitmap()
    {
        if (_gradientBitmap is null) return;
        _effect?.SetGradientInput(_fallbackBitmap);
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
