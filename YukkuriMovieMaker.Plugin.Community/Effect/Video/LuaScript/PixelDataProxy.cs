using System.Diagnostics;
using MoonSharp.Interpreter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    [MoonSharpUserData]
    internal sealed class PixelDataProxy
    {
        private readonly AviUtlScriptContext _ctx;

        internal PixelDataProxy(AviUtlScriptContext ctx) => _ctx = ctx;

        public int width => _ctx.ImageWidth;
        public int height => _ctx.ImageHeight;

        public double get(int index)
        {
            _ctx.EnsurePixelBuffer();
            var buf = _ctx.GetPixelBuffer();
            if (buf is null) return 0d;

            int zeroBasedIndex = index - 1;
            int totalChannels = _ctx.ImageWidth * _ctx.ImageHeight * 4;
            if ((uint)zeroBasedIndex >= (uint)totalChannels) return 0d;

            int pixelIndex = zeroBasedIndex / 4;
            int channel = zeroBasedIndex % 4;
            int bufIndex = pixelIndex * 4;
            double a = buf[bufIndex + 3];

            return channel switch
            {
                0 => a > 0d ? Math.Clamp(buf[bufIndex + 2] * 255d / a, 0d, 255d) : 0d,
                1 => a > 0d ? Math.Clamp(buf[bufIndex + 1] * 255d / a, 0d, 255d) : 0d,
                2 => a > 0d ? Math.Clamp(buf[bufIndex + 0] * 255d / a, 0d, 255d) : 0d,
                3 => a,
                _ => throw new UnreachableException()
            };
        }

        public void set(int index, double value)
        {
            _ctx.EnsurePixelBuffer();
            var buf = _ctx.GetPixelBuffer();
            if (buf is null) return;

            int zeroBasedIndex = index - 1;
            int totalChannels = _ctx.ImageWidth * _ctx.ImageHeight * 4;
            if ((uint)zeroBasedIndex >= (uint)totalChannels) return;

            _ctx.MarkPixelsDirty();

            int pixelIndex = zeroBasedIndex / 4;
            int channel = zeroBasedIndex % 4;
            int bufIndex = pixelIndex * 4;
            double clamped = Math.Clamp(value, 0d, 255d);

            if (channel == 3)
            {
                double oldA = buf[bufIndex + 3];
                double newA = clamped;
                if (oldA > 0d && newA > 0d)
                {
                    double factor = newA / oldA;
                    buf[bufIndex + 0] = (byte)Math.Clamp(buf[bufIndex + 0] * factor, 0d, 255d);
                    buf[bufIndex + 1] = (byte)Math.Clamp(buf[bufIndex + 1] * factor, 0d, 255d);
                    buf[bufIndex + 2] = (byte)Math.Clamp(buf[bufIndex + 2] * factor, 0d, 255d);
                }
                else if (newA <= 0d)
                {
                    buf[bufIndex + 0] = buf[bufIndex + 1] = buf[bufIndex + 2] = 0;
                }
                buf[bufIndex + 3] = (byte)newA;
            }
            else
            {
                double a = buf[bufIndex + 3];
                double premultiplied = clamped * (a / 255d);
                int byteIndex = channel switch { 0 => bufIndex + 2, 1 => bufIndex + 1, 2 => bufIndex + 0, _ => throw new UnreachableException() };
                buf[byteIndex] = (byte)Math.Clamp(premultiplied, 0d, 255d);
            }
        }
    }
}
