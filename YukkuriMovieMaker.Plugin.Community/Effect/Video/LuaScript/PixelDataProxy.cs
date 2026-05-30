using System.Diagnostics;
using MoonSharp.Interpreter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    [MoonSharpUserData]
    internal sealed class PixelDataProxy(AviUtlScriptContext ctx)
    {
        public int width => ctx.ImageWidth;
        public int height => ctx.ImageHeight;

        public unsafe double get(int index)
        {
            ctx.EnsurePixelBuffer();
            var buf = ctx.GetPixelBuffer();
            if (buf is null) return 0d;

            int zeroBasedIndex = index - 1;
            if ((uint)zeroBasedIndex >= (uint)ctx.TotalChannels) return 0d;

            int pixelIndex = Math.DivRem(zeroBasedIndex, 4, out int channel);

            fixed (byte* p = buf)
            {
                byte* pixel = p + pixelIndex * 4;
                double a = pixel[3];

                return channel switch
                {
                    0 => a > 0d ? Math.Clamp(pixel[2] * 255d / a, 0d, 255d) : 0d,
                    1 => a > 0d ? Math.Clamp(pixel[1] * 255d / a, 0d, 255d) : 0d,
                    2 => a > 0d ? Math.Clamp(pixel[0] * 255d / a, 0d, 255d) : 0d,
                    3 => a,
                    _ => throw new UnreachableException()
                };
            }
        }

        public unsafe void set(int index, double value)
        {
            ctx.EnsurePixelBuffer();
            var buf = ctx.GetPixelBuffer();
            if (buf is null) return;

            int zeroBasedIndex = index - 1;
            if ((uint)zeroBasedIndex >= (uint)ctx.TotalChannels) return;

            ctx.MarkPixelsDirty();

            int pixelIndex = Math.DivRem(zeroBasedIndex, 4, out int channel);
            double clamped = Math.Clamp(value, 0d, 255d);

            fixed (byte* p = buf)
            {
                byte* pixel = p + pixelIndex * 4;

                if (channel == 3)
                {
                    double oldA = pixel[3];
                    double newA = clamped;
                    if (oldA > 0d && newA > 0d)
                    {
                        double factor = newA / oldA;
                        pixel[0] = (byte)Math.Clamp(pixel[0] * factor, 0d, 255d);
                        pixel[1] = (byte)Math.Clamp(pixel[1] * factor, 0d, 255d);
                        pixel[2] = (byte)Math.Clamp(pixel[2] * factor, 0d, 255d);
                    }
                    else if (newA <= 0d)
                    {
                        pixel[0] = pixel[1] = pixel[2] = 0;
                    }
                    pixel[3] = (byte)newA;
                }
                else
                {
                    double a = pixel[3];
                    double premultiplied = clamped * (a / 255d);
                    int byteOffset = channel switch { 0 => 2, 1 => 1, 2 => 0, _ => throw new UnreachableException() };
                    pixel[byteOffset] = (byte)Math.Clamp(premultiplied, 0d, 255d);
                }
            }
        }
    }
}
