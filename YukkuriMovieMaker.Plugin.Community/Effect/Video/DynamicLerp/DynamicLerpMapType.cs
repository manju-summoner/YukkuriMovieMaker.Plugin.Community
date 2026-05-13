using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DynamicLerp
{
    public enum DynamicLerpMapType
    {
        [Display(Name = nameof(Texts.MapTypeLuminance), Description = nameof(Texts.MapTypeLuminanceDesc), ResourceType = typeof(Texts))]
        Luminance = 0,

        [Display(Name = nameof(Texts.MapTypeAlpha), Description = nameof(Texts.MapTypeAlphaDesc), ResourceType = typeof(Texts))]
        Alpha = 1,
    }
}
