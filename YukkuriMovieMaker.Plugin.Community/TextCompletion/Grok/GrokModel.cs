namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokModel(string key, bool isReasoningOnly)
    {
        public string Key { get; } = key;
        public bool IsReasoningOnly { get; } = isReasoningOnly;
    }
}
