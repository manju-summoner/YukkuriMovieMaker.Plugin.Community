using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public static class GradientTextureFactory
{
    private static readonly List<IGradientFormatParser> _parsers =
    [
        new GrdFormatParser(),
        new ImageFormatParser(),
    ];

    public static ID2D1Bitmap? CreateGradientBitmap(
        ID2D1DeviceContext deviceContext,
        string filePath,
        int gradientIndex)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            for (var i = 0; i < _parsers.Count; i++)
            {
                if (_parsers[i].CanParse(filePath))
                    return _parsers[i].CreateBitmap(deviceContext, filePath, gradientIndex);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static GrdManifest ReadManifest(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return GrdManifest.Empty;

        try
        {
            for (var i = 0; i < _parsers.Count; i++)
            {
                if (_parsers[i].CanParse(filePath))
                    return _parsers[i].ReadManifest(filePath);
            }
            return GrdManifest.Empty;
        }
        catch
        {
            return GrdManifest.Empty;
        }
    }

    public static ID2D1Bitmap? CreateGradientBitmapFromJson(
        ID2D1DeviceContext deviceContext,
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var stops = GradientStopSerializer.Deserialize(json);
            if (stops.Length < 2) return null;

            var pixels = GradientExportService.RasterizeGradient(stops);
            return CreateD2DBitmap(deviceContext, pixels, GradientExportService.GradientResolution);
        }
        catch
        {
            return null;
        }
    }

    internal static ID2D1Bitmap CreateD2DBitmap(
        ID2D1DeviceContext deviceContext,
        byte[] pixels,
        int width)
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
            return ((ID2D1DeviceContext1)deviceContext).CreateBitmap(
                size, handle.AddrOfPinnedObject(), width * 4, props);
        }
        finally
        {
            handle.Free();
        }
    }
}
