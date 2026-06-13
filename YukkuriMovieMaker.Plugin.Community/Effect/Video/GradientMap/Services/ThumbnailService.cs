using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

internal static class ThumbnailService
{
    private const int ThumbnailWidth = 48;
    private const int ThumbnailHeight = 16;

    internal static Task<BitmapSource?> CreateThumbnailAsync(string filePath, int gradientIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return Task.FromResult<BitmapSource?>(null);

        return Task.Run(() =>
        {
            try
            {
                return string.Equals(
                    Path.GetExtension(filePath), ".grd", StringComparison.OrdinalIgnoreCase)
                    ? CreateGrdThumbnail(filePath, gradientIndex)
                    : CreateImageThumbnail(filePath);
            }
            catch
            {
                return null;
            }
        });
    }

    private static BitmapSource? CreateGrdThumbnail(string filePath, int gradientIndex = 0)
    {
        var pixels = GrdParser.ParseToPixels(filePath, gradientIndex);
        if (pixels is null) return null;

        var thumbPixels = new byte[ThumbnailWidth * ThumbnailHeight * 4];
        for (var y = 0; y < ThumbnailHeight; y++)
        {
            for (var x = 0; x < ThumbnailWidth; x++)
            {
                var srcIdx = (int)(x / (float)ThumbnailWidth * (GrdParser.Resolution - 1)) * 4;
                var dstIdx = (y * ThumbnailWidth + x) * 4;
                thumbPixels[dstIdx + 0] = pixels[srcIdx + 0];
                thumbPixels[dstIdx + 1] = pixels[srcIdx + 1];
                thumbPixels[dstIdx + 2] = pixels[srcIdx + 2];
                thumbPixels[dstIdx + 3] = pixels[srcIdx + 3];
            }
        }

        var bitmap = BitmapSource.Create(
            ThumbnailWidth, ThumbnailHeight, 96, 96,
            PixelFormats.Pbgra32, null,
            thumbPixels, ThumbnailWidth * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource CreateImageThumbnail(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];
        var scale = Math.Min(
            (double)ThumbnailWidth / frame.PixelWidth,
            (double)ThumbnailHeight / frame.PixelHeight);
        var transformed = new TransformedBitmap(
            frame,
            new ScaleTransform(scale, scale));

        var frozen = BitmapFrame.Create(transformed);
        frozen.Freeze();
        return frozen;
    }
}
