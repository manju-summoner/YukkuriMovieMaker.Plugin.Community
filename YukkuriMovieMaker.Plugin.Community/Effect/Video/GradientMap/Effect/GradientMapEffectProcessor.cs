using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

internal sealed class GradientMapEffectProcessor : VideoEffectProcessorBase
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly GradientMapEffect _item;
    private readonly IGradientTextureFactory _textureFactory;

    private GradientMapCustomEffect? _effect;
    private ID2D1Bitmap? _gradientBitmap;
    private ID2D1Bitmap? _fallbackBitmap;

    private string _loadedPath = string.Empty;
    private int _loadedIndex = -1;
    private string _loadedJson = string.Empty;
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
        _textureFactory = GradientMapServices.Container.Resolve<IGradientTextureFactory>();
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var effect = new GradientMapCustomEffect(devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return null;
        }

        _effect = effect;
        disposer.Collect(_effect);

        _fallbackBitmap = CreateFallbackBitmap(devices);
        if (_fallbackBitmap is not null)
        {
            disposer.Collect(_fallbackBitmap);
            _effect.SetGradientInput(_fallbackBitmap);
        }

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
        var isHorizontal = _item.IsHorizontal ? 1 : 0;
        var json = _item.CustomGradientJson ?? string.Empty;
        var path = _item.GradientFilePath ?? string.Empty;
        var gradientIndex = _item.GradientIndex;

        var jsonChanged = !string.Equals(json, _loadedJson, StringComparison.Ordinal);
        var fileChanged = !string.Equals(path, _loadedPath, StringComparison.Ordinal) || gradientIndex != _loadedIndex;

        if (fileChanged && !jsonChanged)
        {
            RefreshGradientBitmapFromFile(path, gradientIndex, json);
        }
        else if (jsonChanged && !fileChanged)
        {
            if (!string.IsNullOrWhiteSpace(json))
                RefreshGradientBitmapFromJson(json, path, gradientIndex);
            else
                RefreshGradientBitmapFromFile(path, gradientIndex, json);
        }
        else if (jsonChanged && fileChanged)
        {
            if (!string.IsNullOrWhiteSpace(json))
                RefreshGradientBitmapFromJson(json, path, gradientIndex);
            else if (!string.IsNullOrWhiteSpace(path))
                RefreshGradientBitmapFromFile(path, gradientIndex, json);
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

    private void RefreshGradientBitmapFromJson(string json, string path, int gradientIndex)
    {
        ReleaseBitmap();
        _loadedJson = json;
        _loadedPath = path;
        _loadedIndex = gradientIndex;

        var bitmap = _textureFactory.CreateGradientBitmapFromJson(_devices.DeviceContext, json);
        if (bitmap is null)
        {
            _effect?.SetGradientInput(_fallbackBitmap);
            return;
        }

        _gradientBitmap = bitmap;
        disposer.Collect(_gradientBitmap);
        _effect?.SetGradientInput(bitmap);
    }

    private void RefreshGradientBitmapFromFile(string path, int gradientIndex, string json)
    {
        ReleaseBitmap();
        _loadedPath = path;
        _loadedIndex = gradientIndex;
        _loadedJson = json;

        var bitmap = _textureFactory.CreateGradientBitmap(_devices.DeviceContext, path, gradientIndex);
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
