namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiModel(string Key, bool IsBeta, int? thinkingBudgetOnSkipReasoning)
    {
        public string Key { get; } = Key;
        public bool IsBeta { get; } = IsBeta;
        public bool CanDisableReasoning => ThinkingBudgetOnSkipReasoning != null;
        public int? ThinkingBudgetOnSkipReasoning { get; } = thinkingBudgetOnSkipReasoning;
    }

}
