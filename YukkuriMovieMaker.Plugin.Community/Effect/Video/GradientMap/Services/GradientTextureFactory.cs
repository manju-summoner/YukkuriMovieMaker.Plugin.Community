using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Interfaces;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

public sealed class GradientTextureFactory : IGradientTextureFactory
{
    public ID2D1Bitmap? CreateGradientBitmap(
        ID2D1DeviceContext deviceContext,
        string filePath,
        int gradientIndex)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            return string.Equals(
                Path.GetExtension(filePath), ".grd", StringComparison.OrdinalIgnoreCase)
                ? CreateFromGrd(deviceContext, filePath, gradientIndex)
                : CreateFromImage(deviceContext, filePath);
        }
        catch
        {
            return null;
        }
    }

    public ID2D1Bitmap? CreateGradientBitmapFromJson(
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
            return CreateBitmapFromPixels(deviceContext, pixels, GradientExportService.GradientResolution);
        }
        catch
        {
            return null;
        }
    }

    private static ID2D1Bitmap? CreateFromGrd(
        ID2D1DeviceContext deviceContext,
        string filePath,
        int gradientIndex)
    {
        var pixels = GrdParser.ParseToPixels(filePath, gradientIndex);
        if (pixels is null) return null;

        return CreateBitmapFromPixels(deviceContext, pixels, GrdParser.Resolution);
    }

    private static ID2D1Bitmap CreateBitmapFromPixels(
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

    private static ID2D1Bitmap? CreateFromImage(ID2D1DeviceContext deviceContext, string filePath)
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
        BitmapSource? result = null;

        void Load()
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
            result = converted;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            dispatcher.Invoke(Load);
        else
            Load();

        return result ?? throw new InvalidOperationException("Bitmap load returned null.");
    }
}
