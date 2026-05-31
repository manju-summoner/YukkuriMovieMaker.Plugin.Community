using MoonSharp.Interpreter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal static class AnimTableRegistrar
    {
        private static readonly double s_tau = Math.PI * 2d;

        internal static void RegisterFunctions(Table anim)
        {
            anim["tau"] = s_tau;

            anim["lerp"] = DynValue.NewCallback((_, args) =>
            {
                double a = args[0].CastToNumber() ?? 0d;
                double b = args[1].CastToNumber() ?? 0d;
                double t = args[2].CastToNumber() ?? 0d;
                return DynValue.NewNumber(a + (b - a) * t);
            });

            anim["smoothstep"] = DynValue.NewCallback((_, args) =>
            {
                double edge0 = args[0].CastToNumber() ?? 0d;
                double edge1 = args[1].CastToNumber() ?? 1d;
                double x = args[2].CastToNumber() ?? 0d;
                double span = edge1 - edge0;
                double t = span == 0d ? 0d : Math.Clamp((x - edge0) / span, 0d, 1d);
                return DynValue.NewNumber(t * t * (3d - 2d * t));
            });

            anim["clamp"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                double lo = args[1].CastToNumber() ?? 0d;
                double hi = args[2].CastToNumber() ?? 1d;
                return DynValue.NewNumber(Math.Clamp(v, lo, hi));
            });

            anim["map"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                double a1 = args[1].CastToNumber() ?? 0d;
                double b1 = args[2].CastToNumber() ?? 1d;
                double a2 = args[3].CastToNumber() ?? 0d;
                double b2 = args[4].CastToNumber() ?? 1d;
                double range = b1 - a1;
                if (range == 0d) return DynValue.NewNumber(a2);
                return DynValue.NewNumber(a2 + (b2 - a2) * (v - a1) / range);
            });

            anim["norm"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                double lo = args[1].CastToNumber() ?? 0d;
                double hi = args[2].CastToNumber() ?? 1d;
                double span = hi - lo;
                if (span == 0d) return DynValue.NewNumber(0d);
                return DynValue.NewNumber((v - lo) / span);
            });

            anim["wrap"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                double lo = args[1].CastToNumber() ?? 0d;
                double hi = args[2].CastToNumber() ?? 1d;
                double range = hi - lo;
                if (range <= 0d) return DynValue.NewNumber(lo);
                double result = (v - lo) % range;
                if (result < 0d) result += range;
                return DynValue.NewNumber(lo + result);
            });

            anim["pingpong"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double length = args[1].CastToNumber() ?? 1d;
                if (length <= 0d) return DynValue.NewNumber(0d);
                double v = t % (2d * length);
                if (v < 0d) v += 2d * length;
                return DynValue.NewNumber(v > length ? 2d * length - v : v);
            });

            anim["sign"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                return DynValue.NewNumber(Math.Sign(v));
            });

            anim["oscillate"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double lo = args[1].CastToNumber() ?? 0d;
                double hi = args[2].CastToNumber() ?? 1d;
                double freq = args[3].CastToNumber() ?? 1d;
                double wave = (Math.Sin(t * freq * s_tau) + 1d) / 2d;
                return DynValue.NewNumber(lo + (hi - lo) * wave);
            });

            anim["triangle"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double freq = args[1].CastToNumber() ?? 1d;
                double f = t * freq;
                f -= Math.Floor(f);
                return DynValue.NewNumber(1d - 2d * Math.Abs(f - 0.5d));
            });

            anim["square"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double freq = args[1].CastToNumber() ?? 1d;
                double f = t * freq;
                f -= Math.Floor(f);
                return DynValue.NewNumber(f >= 0.5d ? 1d : 0d);
            });

            anim["duration"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double dur = args[1].CastToNumber() ?? 1d;
                if (dur <= 0d) return DynValue.NewNumber(1d);
                return DynValue.NewNumber(Math.Clamp(t / dur, 0d, 1d));
            });

            anim["delay"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double d = args[1].CastToNumber() ?? 0d;
                return DynValue.NewNumber(Math.Max(0d, t - d));
            });

            anim["ease_in"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                return DynValue.NewNumber(t * t);
            });

            anim["ease_out"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double inv = 1d - t;
                return DynValue.NewNumber(1d - inv * inv);
            });

            anim["elastic"] = DynValue.NewCallback((_, args) =>
            {
                double t = Math.Clamp(args[0].CastToNumber() ?? 0d, 0d, 1d);
                if (t == 0d) return DynValue.NewNumber(0d);
                if (t == 1d) return DynValue.NewNumber(1d);
                return DynValue.NewNumber(Math.Pow(2d, -10d * t) * Math.Sin((t * 10d - 0.75d) * (s_tau / 3d)) + 1d);
            });

            anim["back"] = DynValue.NewCallback((_, args) =>
            {
                const double c1 = 1.70158d;
                const double c3 = c1 + 1d;
                double t = args[0].CastToNumber() ?? 0d;
                double inv = t - 1d;
                return DynValue.NewNumber(1d + c3 * inv * inv * inv + c1 * inv * inv);
            });

            anim["step"] = DynValue.NewCallback((_, args) =>
            {
                double edge = args[0].CastToNumber() ?? 0d;
                double x = args[1].CastToNumber() ?? 0d;
                return DynValue.NewNumber(x >= edge ? 1d : 0d);
            });

            anim["fract"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                return DynValue.NewNumber(v - Math.Floor(v));
            });

            anim["bounce"] = DynValue.NewCallback((_, args) =>
            {
                const double n1 = 7.5625d;
                const double d1 = 2.75d;
                double t = Math.Clamp(args[0].CastToNumber() ?? 0d, 0d, 1d);
                if (t < 1d / d1)
                    return DynValue.NewNumber(n1 * t * t);
                if (t < 2d / d1)
                {
                    t -= 1.5d / d1;
                    return DynValue.NewNumber(n1 * t * t + 0.75d);
                }
                if (t < 2.5d / d1)
                {
                    t -= 2.25d / d1;
                    return DynValue.NewNumber(n1 * t * t + 0.9375d);
                }
                t -= 2.625d / d1;
                return DynValue.NewNumber(n1 * t * t + 0.984375d);
            });

            anim["hsv_to_rgb"] = DynValue.NewCallback((_, args) =>
            {
                double h = args[0].CastToNumber() ?? 0d;
                double s = args[1].CastToNumber() ?? 0d;
                double v = args[2].CastToNumber() ?? 0d;
                double c = v * s;
                double x = c * (1d - Math.Abs((h / 60d) % 2d - 1d));
                double m = v - c;
                int sector = ((int)Math.Floor(h / 60d) % 6 + 6) % 6;
                (double r, double g, double b) = sector switch
                {
                    0 => (c, x, 0d),
                    1 => (x, c, 0d),
                    2 => (0d, c, x),
                    3 => (0d, x, c),
                    4 => (x, 0d, c),
                    _ => (c, 0d, x),
                };
                return DynValue.NewTuple(
                    DynValue.NewNumber((r + m) * 255d),
                    DynValue.NewNumber((g + m) * 255d),
                    DynValue.NewNumber((b + m) * 255d));
            });

            anim["rgb_to_hsv"] = DynValue.NewCallback((_, args) =>
            {
                double r = (args[0].CastToNumber() ?? 0d) / 255d;
                double g = (args[1].CastToNumber() ?? 0d) / 255d;
                double b = (args[2].CastToNumber() ?? 0d) / 255d;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                double delta = max - min;
                double h = 0d;
                if (delta > 0d)
                {
                    if (max == r) h = 60d * ((((g - b) / delta) % 6d + 6d) % 6d);
                    else if (max == g) h = 60d * ((b - r) / delta + 2d);
                    else h = 60d * ((r - g) / delta + 4d);
                }
                return DynValue.NewTuple(
                    DynValue.NewNumber(h),
                    DynValue.NewNumber(max > 0d ? delta / max : 0d),
                    DynValue.NewNumber(max));
            });
        }
    }
}
