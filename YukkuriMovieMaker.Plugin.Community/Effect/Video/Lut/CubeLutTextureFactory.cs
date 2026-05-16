using System.Runtime.InteropServices;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

internal static class CubeLutTextureFactory
{
    public static ID2D1Bitmap? CreateAtlas(ID2D1DeviceContext deviceContext, CubeLut lut)
    {
        if (lut.Size3D < 2 || lut.Data3D.Length != lut.Size3D * lut.Size3D * lut.Size3D * 3)
            return null;

        try
        {
            var n = lut.Size3D;
            var width = n * n;
            var height = n;
            var pixels = new ushort[width * height * 4];
            var data = lut.HasShaper ? ComposeWithShaper(lut) : lut.Data3D;

            for (var b = 0; b < n; b++)
            {
                for (var g = 0; g < n; g++)
                {
                    for (var r = 0; r < n; r++)
                    {
                        var srcIndex = (r + g * n + b * n * n) * 3;
                        var dstX = b * n + r;
                        var dstY = g;
                        var dstIndex = (dstY * width + dstX) * 4;

                        pixels[dstIndex + 0] = ToHalf(data[srcIndex + 0]);
                        pixels[dstIndex + 1] = ToHalf(data[srcIndex + 1]);
                        pixels[dstIndex + 2] = ToHalf(data[srcIndex + 2]);
                        pixels[dstIndex + 3] = OneHalf;
                    }
                }
            }

            return CreateD2DBitmap(deviceContext, pixels, width, height);
        }
        catch
        {
            return null;
        }
    }

    public static ID2D1Bitmap? CreateIdentityAtlas(ID2D1DeviceContext deviceContext)
    {
        try
        {
            const int n = 2;
            const int width = n * n;
            const int height = n;
            var pixels = new ushort[width * height * 4];

            for (var b = 0; b < n; b++)
            {
                for (var g = 0; g < n; g++)
                {
                    for (var r = 0; r < n; r++)
                    {
                        var dstX = b * n + r;
                        var dstY = g;
                        var dstIndex = (dstY * width + dstX) * 4;

                        pixels[dstIndex + 0] = r == 0 ? ZeroHalf : OneHalf;
                        pixels[dstIndex + 1] = g == 0 ? ZeroHalf : OneHalf;
                        pixels[dstIndex + 2] = b == 0 ? ZeroHalf : OneHalf;
                        pixels[dstIndex + 3] = OneHalf;
                    }
                }
            }

            return CreateD2DBitmap(deviceContext, pixels, width, height);
        }
        catch
        {
            return null;
        }
    }

    private static float[] ComposeWithShaper(CubeLut lut)
    {
        var n = lut.Size3D;
        var composed = new float[n * n * n * 3];
        var last = n - 1f;

        for (var b = 0; b < n; b++)
        {
            for (var g = 0; g < n; g++)
            {
                for (var r = 0; r < n; r++)
                {
                    var (cr, cg, cb) = CubeLutSampler.SampleNormalized(lut, r / last, g / last, b / last);
                    var i = (r + g * n + b * n * n) * 3;
                    composed[i] = cr;
                    composed[i + 1] = cg;
                    composed[i + 2] = cb;
                }
            }
        }

        return composed;
    }

    private const ushort ZeroHalf = 0x0000;
    private const ushort OneHalf = 0x3C00;

    private static ushort ToHalf(float value) => BitConverter.HalfToUInt16Bits((Half)value);

    private static ID2D1Bitmap CreateD2DBitmap(ID2D1DeviceContext deviceContext, ushort[] pixels, int width, int height)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var size = new Vortice.Mathematics.SizeI(width, height);
            var props = new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(
                    Vortice.DXGI.Format.R16G16B16A16_Float,
                    Vortice.DCommon.AlphaMode.Premultiplied),
                96f, 96f, BitmapOptions.None);
            return ((ID2D1DeviceContext1)deviceContext).CreateBitmap(
                size, handle.AddrOfPinnedObject(), width * 8, props);
        }
        finally
        {
            handle.Free();
        }
    }
}
