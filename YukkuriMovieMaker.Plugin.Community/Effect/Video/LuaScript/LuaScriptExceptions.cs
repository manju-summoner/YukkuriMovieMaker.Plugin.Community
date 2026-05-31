namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal abstract class LuaScriptException(string message, Exception? inner = null) : Exception(message, inner);

    internal sealed class LuaScriptCompilationException(string message, Exception? inner = null) : LuaScriptException(message, inner);

    internal sealed class LuaScriptRuntimeException(string message, Exception? inner = null) : LuaScriptException(message, inner);
}
