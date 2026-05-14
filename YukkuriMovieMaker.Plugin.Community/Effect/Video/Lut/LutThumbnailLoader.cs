using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

public sealed class LutThumbnailLoader : IFileSelectorThumbnailLoader
{
    private const int BandWidth = 256;
    private const int BandHeight = 64;

    public bool CanLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        return LutParserRegistry.CanParse(filePath);
    }

    public Task<BitmapSource?> LoadThumbnailAsync(string filePath)
    {
        return Task.Run<BitmapSource?>(() =>
        {
            try
            {
                var lut = LutParserRegistry.Parse(filePath);
                if (lut is null)
                    return null;

                var rowPixels = RenderDiagonalBand(lut);
                var rowStride = BandWidth * 4;
                var buffer = new byte[rowStride * BandHeight];
                for (var y = 0; y < BandHeight; y++)
                    System.Buffer.BlockCopy(rowPixels, 0, buffer, y * rowStride, rowStride);

                var bitmap = BitmapSource.Create(
                    BandWidth, BandHeight, 96, 96,
                    PixelFormats.Pbgra32, null,
                    buffer, rowStride);
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        });
    }

    private static byte[] RenderDiagonalBand(CubeLut lut)
    {
        var pixels = new byte[BandWidth * 4];
        var last = BandWidth - 1f;

        for (var x = 0; x < BandWidth; x++)
        {
            var t = x / last;
            var (r, g, b) = CubeLutSampler.Sample(lut, t, t, t);
            pixels[x * 4 + 0] = ToByte(b);
            pixels[x * 4 + 1] = ToByte(g);
            pixels[x * 4 + 2] = ToByte(r);
            pixels[x * 4 + 3] = 255;
        }

        return pixels;
    }

    private static byte ToByte(float v)
    {
        if (v <= 0f) return 0;
        if (v >= 1f) return 255;
        return (byte)MathF.Round(v * 255f);
    }
}
