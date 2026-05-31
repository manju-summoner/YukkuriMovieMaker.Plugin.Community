using MoonSharp.Interpreter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal static class Ymm4TableRegistrar
    {
        internal static void RegisterFunctions(Table ymm4)
        {
            ymm4["lerp"] = DynValue.NewCallback((_, args) =>
            {
                double a = args[0].CastToNumber() ?? 0d;
                double b = args[1].CastToNumber() ?? 0d;
                double t = args[2].CastToNumber() ?? 0d;
                return DynValue.NewNumber(a + (b - a) * t);
            });

            ymm4["smoothstep"] = DynValue.NewCallback((_, args) =>
            {
                double edge0 = args[0].CastToNumber() ?? 0d;
                double edge1 = args[1].CastToNumber() ?? 1d;
                double x = args[2].CastToNumber() ?? 0d;
                double span = edge1 - edge0;
                double t = span == 0d ? 0d : Math.Clamp((x - edge0) / span, 0d, 1d);
                return DynValue.NewNumber(t * t * (3d - 2d * t));
            });

            ymm4["clamp"] = DynValue.NewCallback((_, args) =>
            {
                double v = args[0].CastToNumber() ?? 0d;
                double lo = args[1].CastToNumber() ?? 0d;
                double hi = args[2].CastToNumber() ?? 1d;
                return DynValue.NewNumber(Math.Clamp(v, lo, hi));
            });

            ymm4["map"] = DynValue.NewCallback((_, args) =>
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

            ymm4["pingpong"] = DynValue.NewCallback((_, args) =>
            {
                double t = args[0].CastToNumber() ?? 0d;
                double length = args[1].CastToNumber() ?? 1d;
                if (length <= 0d) return DynValue.NewNumber(0d);
                double v = t % (2d * length);
                if (v < 0d) v += 2d * length;
                return DynValue.NewNumber(v > length ? 2d * length - v : v);
            });
        }

        internal static void UpdateVariables(Table ymm4, AviUtlScriptContext ctx)
        {
            ymm4["group_index"] = ctx.GroupIndex;
            ymm4["group_count"] = ctx.GroupCount;
            ymm4["timeline_totalframe"] = ctx.TimelineTotalFrame;
            ymm4["timeline_totaltime"] = ctx.TimelineTotalTime;
            ymm4["is_saving"] = ctx.IsSaving;
            ymm4["time_ratio"] = ctx.TimeRatio;
        }
    }
}
