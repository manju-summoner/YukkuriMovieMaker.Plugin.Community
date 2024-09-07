using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorBlindness
{
    public enum ColorBlindnessType
    {
        [Display(Name = nameof(Texts.ColorBlindnessTypeP), ResourceType = typeof(Texts))]
        P = 1,
        [Display(Name = nameof(Texts.ColorBlindnessTypeD), ResourceType = typeof(Texts))]
        D = 2,
        [Display(Name = nameof(Texts.ColorBlindnessTypeT), ResourceType = typeof(Texts))]
        T = 3,
    }
}
