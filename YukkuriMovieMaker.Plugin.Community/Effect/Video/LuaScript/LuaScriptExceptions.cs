namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal sealed class LuaScriptCompilationException : Exception
    {
        public LuaScriptCompilationException(string message) : base(message) { }
        public LuaScriptCompilationException(string message, Exception inner) : base(message, inner) { }
    }

    internal sealed class LuaScriptRuntimeException : Exception
    {
        public LuaScriptRuntimeException(string message) : base(message) { }
        public LuaScriptRuntimeException(string message, Exception inner) : base(message, inner) { }
    }
}
