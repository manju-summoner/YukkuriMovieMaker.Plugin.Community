using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LongShadow
{
    public enum LongShadowType
    {
        [Display(Name = nameof(Texts.LongShadowTypeImageName), Description = nameof(Texts.LongShadowTypeImageDesc), ResourceType = typeof(Texts))]
        Image = 1,
        [Display(Name = nameof(Texts.LongShadowTypeSolidName), Description = nameof(Texts.LongShadowTypeSolidDesc), ResourceType = typeof(Texts))]
        Solid = 2,
        [Display(Name = nameof(Texts.LongShadowTypeGradientName), Description = nameof(Texts.LongShadowTypeGradientDesc), ResourceType = typeof(Texts))]
        Gradient = 4,
    }
}
