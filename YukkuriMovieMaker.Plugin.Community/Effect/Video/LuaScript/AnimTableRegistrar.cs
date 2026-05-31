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
        }
    }
}
