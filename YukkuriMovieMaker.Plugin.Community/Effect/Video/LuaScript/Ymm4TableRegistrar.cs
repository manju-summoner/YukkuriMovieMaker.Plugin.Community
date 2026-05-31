using MoonSharp.Interpreter;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal static class Ymm4TableRegistrar
    {
        internal static void UpdateVariables(Table ymm4, AviUtlScriptContext ctx)
        {
            ymm4["group_index"] = ctx.GroupIndex;
            ymm4["group_count"] = ctx.GroupCount;
            ymm4["group_ratio"] = ctx.GroupCount > 0 ? ctx.GroupIndex / (double)ctx.GroupCount : 0d;
            ymm4["timeline_totalframe"] = ctx.TimelineTotalFrame;
            ymm4["timeline_totaltime"] = ctx.TimelineTotalTime;
            ymm4["is_saving"] = ctx.IsSaving;
            ymm4["time_ratio"] = ctx.TimeRatio;
        }
    }
}
