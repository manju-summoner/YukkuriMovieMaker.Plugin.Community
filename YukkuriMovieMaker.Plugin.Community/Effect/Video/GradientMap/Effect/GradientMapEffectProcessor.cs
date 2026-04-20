using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

public sealed class GradientMapEffectProcessor : IVideoEffectProcessor
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly GradientMapEffect _item;
    private readonly IResourceRegistry _registry;
    private readonly IGradientTextureFactory _textureFactory;

    private GradientMapCustomEffect? _effect;
    private ID2D1Bitmap? _gradientBitmap;
    private ID2D1Bitmap? _fallbackBitmap;
    private ID2D1Image? _sourceInput;

    private string _loadedPath = string.Empty;
    private int _loadedIndex = -1;
    private string _loadedJson = string.Empty;
    private bool _isFirst = true;
    private float _opacity;
    private int _blendMode;
    private int _isHorizontal;

    public ID2D1Image Output
    {
        get
        {
            if (_effect is not null && _effect.IsEnabled)
                return _effect.Output;
            return _sourceInput
                ?? throw new InvalidOperationException(
                            "SetInput must be called before accessing Output.");
                        }
                    }

                    public GradientMapEffectProcessor(
                        IGraphicsDevicesAndContext devices,
                        GradientMapEffect item)
                    {
                        _devices = devices;
                        _item = item;
                        _registry = GradientMapServices.Container.Resolve<IResourceRegistry>();
                        _textureFactory = GradientMapServices.Container.Resolve<IGradientTextureFactory>();

                        InitializeEffect();
                    }

    private void InitializeEffect()
    {
        var effect = new GradientMapCustomEffect(_devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return;
        }

        _effect = effect;
        _fallbackBitmap = CreateFallbackBitmap();
        if (_fallbackBitmap is not null)
            _effect.SetGradientInput(_fallbackBitmap);
    }

    private ID2D1Bitmap? CreateFallbackBitmap()
    {
        try
        {
            var stops = new GradientColorStop[]
            {
                new(0f, 0, 0, 0, 255),
                new(1f, 255, 255, 255, 255),
            };
            var pixels = GradientExportService.RasterizeGradient(stops);
            return CreateD2DBitmap(pixels, GradientExportService.GradientResolution);
        }
        catch
        {
            return null;
        }
    }

    private ID2D1Bitmap CreateD2DBitmap(byte[] pixels, int width)
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
            return ((ID2D1DeviceContext1)_devices.DeviceContext).CreateBitmap(
                size, handle.AddrOfPinnedObject(), width * 4, props);
        }
        finally
        {
            handle.Free();
        }
    }

    public void SetInput(ID2D1Image? input)
    {
        _sourceInput = input;
        _effect?.SetSourceInput(input);
    }

    public void ClearInput()
    {
        _sourceInput = null;
        _effect?.SetSourceInput(null);
        if (_fallbackBitmap is not null)
            _effect?.SetGradientInput(_fallbackBitmap);
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        if (_effect is null)
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

        _gradientBitmap = _registry.Track(bitmap);
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

        _gradientBitmap = _registry.Track(bitmap);
        _effect?.SetGradientInput(bitmap);
    }

    private void ReleaseBitmap()
    {
        if (_gradientBitmap is null) return;
        _effect?.SetGradientInput(_fallbackBitmap);
        _registry.Untrack(_gradientBitmap);
        _gradientBitmap.Dispose();
        _gradientBitmap = null;
    }

    public void Dispose()
    {
        ReleaseBitmap();
        _effect?.SetSourceInput(null);
        _effect?.Dispose();
        _effect = null;
        _fallbackBitmap?.Dispose();
        _fallbackBitmap = null;
        _registry.Dispose();
    }
}
