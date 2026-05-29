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

            int i = index - 1;
            int totalCh = _ctx.ImageWidth * _ctx.ImageHeight * 4;
            if ((uint)i >= (uint)totalCh) return 0d;

            int pixelIdx = i / 4;
            int channel = i % 4;
            int bufIdx = pixelIdx * 4;

            double a = buf[bufIdx + 3];

            return channel switch
            {
                0 => a > 0d ? Math.Clamp(buf[bufIdx + 2] * 255d / a, 0d, 255d) : 0d,
                1 => a > 0d ? Math.Clamp(buf[bufIdx + 1] * 255d / a, 0d, 255d) : 0d,
                2 => a > 0d ? Math.Clamp(buf[bufIdx + 0] * 255d / a, 0d, 255d) : 0d,
                3 => a,
                _ => 0d
            };
        }

        public void set(int index, double value)
        {
            _ctx.EnsurePixelBuffer();
            var buf = _ctx.GetPixelBuffer();
            if (buf is null) return;

            int i = index - 1;
            int totalCh = _ctx.ImageWidth * _ctx.ImageHeight * 4;
            if ((uint)i >= (uint)totalCh) return;

            _ctx.MarkPixelsDirty();

            int pixelIdx = i / 4;
            int channel = i % 4;
            int bufIdx = pixelIdx * 4;
            double v = Math.Clamp(value, 0d, 255d);

            if (channel == 3)
            {
                double oldA = buf[bufIdx + 3];
                double newA = v;
                if (oldA > 0d && newA > 0d)
                {
                    double factor = newA / oldA;
                    buf[bufIdx + 0] = (byte)Math.Clamp(buf[bufIdx + 0] * factor, 0d, 255d);
                    buf[bufIdx + 1] = (byte)Math.Clamp(buf[bufIdx + 1] * factor, 0d, 255d);
                    buf[bufIdx + 2] = (byte)Math.Clamp(buf[bufIdx + 2] * factor, 0d, 255d);
                }
                else if (newA <= 0d)
                {
                    buf[bufIdx + 0] = buf[bufIdx + 1] = buf[bufIdx + 2] = 0;
                }
                buf[bufIdx + 3] = (byte)newA;
            }
            else
            {
                double a = buf[bufIdx + 3];
                double pv = v * (a / 255d);
                int bi = channel switch { 0 => bufIdx + 2, 1 => bufIdx + 1, 2 => bufIdx + 0, _ => bufIdx + 0 };
                buf[bi] = (byte)Math.Clamp(pv, 0d, 255d);
            }
        }
    }
}
