namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal abstract class LuaScriptException : Exception
    {
        protected LuaScriptException(string message) : base(message) { }
        protected LuaScriptException(string message, Exception inner) : base(message, inner) { }
    }

    internal sealed class LuaScriptCompilationException : LuaScriptException
    {
        public LuaScriptCompilationException(string message) : base(message) { }
        public LuaScriptCompilationException(string message, Exception inner) : base(message, inner) { }
    }

    internal sealed class LuaScriptRuntimeException : LuaScriptException
    {
        public LuaScriptRuntimeException(string message) : base(message) { }
        public LuaScriptRuntimeException(string message, Exception inner) : base(message, inner) { }
    }
}
