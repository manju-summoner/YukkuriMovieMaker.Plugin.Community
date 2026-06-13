using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.CodexCLI
{
    internal enum ReasoningEffort
    {
        [Display(Name = nameof(Texts.ReasoningEffortLow), ResourceType = typeof(Texts))]
        Low,

        [Display(Name = nameof(Texts.ReasoningEffortMedium), ResourceType = typeof(Texts))]
        Medium,

        [Display(Name = nameof(Texts.ReasoningEffortHigh), ResourceType = typeof(Texts))]
        High,
    }
}
