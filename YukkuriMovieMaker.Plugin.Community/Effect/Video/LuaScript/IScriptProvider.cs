namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuaScript
{
    internal interface IScriptProvider
    {
        string Script { get; set; }
        string DefaultScript { get; }
    }
}
