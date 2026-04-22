using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public sealed class ImageFormatParser : IGradientFormatParser
{
    public GrdManifest ReadManifest(string filePath) => GrdManifest.Empty;


    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff",
    };

    public bool CanParse(string filePath) =>
        SupportedExtensions.Contains(Path.GetExtension(filePath));

    public static bool IsImageFile(string filePath) =>
        !string.IsNullOrEmpty(filePath) &&
        SupportedExtensions.Contains(Path.GetExtension(filePath));

    public ID2D1Bitmap? CreateBitmap(
        ID2D1DeviceContext deviceContext,
        string filePath,
        int gradientIndex)
    {
        var source = LoadBitmapSource(filePath);
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var byteLen = stride * height;

        var buffer = ArrayPool<byte>.Shared.Rent(byteLen);
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            source.CopyPixels(buffer, stride, 0);
            var size = new Vortice.Mathematics.SizeI(width, height);
            var props = new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(
                    Vortice.DXGI.Format.B8G8R8A8_UNorm,
                    Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f, BitmapOptions.None);
            return ((ID2D1DeviceContext1)deviceContext).CreateBitmap(
                size, handle.AddrOfPinnedObject(), stride, props);
        }
        finally
        {
            handle.Free();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static BitmapSource LoadBitmapSource(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(
            decoder.Frames[0],
            PixelFormats.Pbgra32,
            null,
            0.0);
        converted.Freeze();
        return converted;
    }
}
