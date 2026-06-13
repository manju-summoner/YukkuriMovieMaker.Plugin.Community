using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.ClaudeCode
{
    internal enum EffortLevel
    {
        [Display(Name = nameof(Texts.EffortLow), ResourceType = typeof(Texts))]
        Low,

        [Display(Name = nameof(Texts.EffortMedium), ResourceType = typeof(Texts))]
        Medium,

        [Display(Name = nameof(Texts.EffortHigh), ResourceType = typeof(Texts))]
        High,

        [Display(Name = nameof(Texts.EffortMax), ResourceType = typeof(Texts))]
        Max,
    }
}
