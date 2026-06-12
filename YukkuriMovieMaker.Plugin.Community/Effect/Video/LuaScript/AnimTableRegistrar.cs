using MoonSharp.Interpreter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal static class AnimTableRegistrar
    {
        private static readonly double s_tau = Math.PI * 2d;
        private static readonly double s_e = Math.E;
        private static readonly double s_phi = (1d + Math.Sqrt(5d)) / 2d;
        private static readonly double s_sqrt2 = Math.Sqrt(2d);

        internal static void RegisterFunctions(Table anim)
        {
            anim["tau"] = s_tau;
            anim["e"] = s_e;
            anim["phi"] = s_phi;
            anim["sqrt2"] = s_sqrt2;

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

            anim["smootherstep"] = DynValue.NewCallback((_, args) =>
            {
                double edge0 = args[0].CastToNumber() ?? 0d;
                double edge1 = args[1].CastToNumber() ?? 1d;
                double x = args[2].CastToNumber() ?? 0d;
                double span = edge1 - edge0;
                double t = span == 0d ? 0d : Math.Clamp((x - edge0) / span, 0d, 1d);
                return DynValue.NewNumber(t * t * t * (t * (6d * t - 15d) + 10d));
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

            anim["ease_in_out"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                if (t < 0.5d)
                    return DynValue.NewNumber(2d * t * t);
                double inv = -2d * t + 2d;
                return DynValue.NewNumber(1d - inv * inv / 2d);
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

            anim["len"] = DynValue.NewCallback((_, args) =>
            {
                double x = args[0].CastToNumber() ?? 0d;
                double y = args[1].CastToNumber() ?? 0d;
                return DynValue.NewNumber(Math.Sqrt(x * x + y * y));
            });

            anim["dist"] = DynValue.NewCallback((_, args) =>
            {
                double x1 = args[0].CastToNumber() ?? 0d;
                double y1 = args[1].CastToNumber() ?? 0d;
                double x2 = args[2].CastToNumber() ?? 0d;
                double y2 = args[3].CastToNumber() ?? 0d;
                double dx = x2 - x1;
                double dy = y2 - y1;
                return DynValue.NewNumber(Math.Sqrt(dx * dx + dy * dy));
            });

            anim["dot"] = DynValue.NewCallback((_, args) =>
            {
                double x1 = args[0].CastToNumber() ?? 0d;
                double y1 = args[1].CastToNumber() ?? 0d;
                double x2 = args[2].CastToNumber() ?? 0d;
                double y2 = args[3].CastToNumber() ?? 0d;
                return DynValue.NewNumber(x1 * x2 + y1 * y2);
            });

            anim["normalize"] = DynValue.NewCallback((_, args) =>
            {
                double x = args[0].CastToNumber() ?? 0d;
                double y = args[1].CastToNumber() ?? 0d;
                double magnitude = Math.Sqrt(x * x + y * y);
                if (magnitude == 0d)
                    return DynValue.NewTuple(DynValue.NewNumber(0d), DynValue.NewNumber(0d));
                return DynValue.NewTuple(
                    DynValue.NewNumber(x / magnitude),
                    DynValue.NewNumber(y / magnitude));
            });

            anim["polar"] = DynValue.NewCallback((_, args) =>
            {
                double r = args[0].CastToNumber() ?? 0d;
                double a = args[1].CastToNumber() ?? 0d;
                double rad = a * Math.PI / 180d;
                return DynValue.NewTuple(
                    DynValue.NewNumber(r * Math.Cos(rad)),
                    DynValue.NewNumber(r * Math.Sin(rad))
                );
            });

            anim["rotate"] = DynValue.NewCallback((_, args) =>
            {
                double x = args[0].CastToNumber() ?? 0d;
                double y = args[1].CastToNumber() ?? 0d;
                double a = args[2].CastToNumber() ?? 0d;
                double rad = a * Math.PI / 180d;
                double c = Math.Cos(rad);
                double s = Math.Sin(rad);
                return DynValue.NewTuple(
                    DynValue.NewNumber(x * c - y * s),
                    DynValue.NewNumber(x * s + y * c)
                );
            });

            anim["bezier"] = DynValue.NewCallback((_, args) =>
            {
                double t = Math.Clamp(args[0].CastToNumber() ?? 0d, 0d, 1d);
                double p0 = args[1].CastToNumber() ?? 0d;
                double p1 = args[2].CastToNumber() ?? 0d;
                double p2 = args[3].CastToNumber() ?? 1d;
                double p3 = args[4].CastToNumber() ?? 1d;

                double inv = 1d - t;
                double b0 = inv * inv * inv;
                double b1 = 3d * inv * inv * t;
                double b2 = 3d * inv * t * t;
                double b3 = t * t * t;

                return DynValue.NewNumber(b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3);
            });

            anim["rand"] = DynValue.NewCallback((_, args) =>
            {
                if (args.Count == 0) return DynValue.NewNumber(0d);
                double seed, min = 0d, max = 1d;
                if (args.Count == 1)
                {
                    seed = args[0].CastToNumber() ?? 0d;
                }
                else if (args.Count == 2)
                {
                    max = args[0].CastToNumber() ?? 1d;
                    seed = args[1].CastToNumber() ?? 0d;
                }
                else
                {
                    min = args[0].CastToNumber() ?? 0d;
                    max = args[1].CastToNumber() ?? 1d;
                    seed = args[2].CastToNumber() ?? 0d;
                }
                long bits = BitConverter.DoubleToInt64Bits(seed);
                uint h = (uint)((bits ^ (bits >> 32)) * 374761393);
                h = (h ^ (h >> 13)) * 1274126177;
                double r = (double)(h ^ (h >> 16)) / 4294967295.0;
                return DynValue.NewNumber(min + r * (max - min));
            });

            anim["noise"] = DynValue.NewCallback((_, args) =>
            {
                double x = args[0].CastToNumber() ?? 0d;
                double y = args.Count > 1 ? args[1].CastToNumber() ?? 0d : 0d;
                double z = args.Count > 2 ? args[2].CastToNumber() ?? 0d : 0d;

                int xi = (int)Math.Floor(x);
                int yi = (int)Math.Floor(y);
                int zi = (int)Math.Floor(z);

                double xf = x - xi;
                double yf = y - yi;
                double zf = z - zi;

                double u = xf * xf * (3d - 2d * xf);
                double v = yf * yf * (3d - 2d * yf);
                double w = zf * zf * (3d - 2d * zf);

                double c000 = GetNoiseHash(xi, yi, zi);
                double c100 = GetNoiseHash(xi + 1, yi, zi);
                double c010 = GetNoiseHash(xi, yi + 1, zi);
                double c110 = GetNoiseHash(xi + 1, yi + 1, zi);
                double c001 = GetNoiseHash(xi, yi, zi + 1);
                double c101 = GetNoiseHash(xi + 1, yi, zi + 1);
                double c011 = GetNoiseHash(xi, yi + 1, zi + 1);
                double c111 = GetNoiseHash(xi + 1, yi + 1, zi + 1);

                double x00 = c000 + u * (c100 - c000);
                double x10 = c010 + u * (c110 - c010);
                double x01 = c001 + u * (c101 - c001);
                double x11 = c011 + u * (c111 - c011);

                double y0 = x00 + v * (x10 - x00);
                double y1 = x01 + v * (x11 - x01);

                return DynValue.NewNumber(y0 + w * (y1 - y0));
            });
        }

        private static double GetNoiseHash(int x, int y, int z)
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + z * 1013904223);
            n = (n ^ (n >> 13)) * 1274126177;
            return (double)(n ^ (n >> 16)) / 4294967295.0;
        }
    }
}
