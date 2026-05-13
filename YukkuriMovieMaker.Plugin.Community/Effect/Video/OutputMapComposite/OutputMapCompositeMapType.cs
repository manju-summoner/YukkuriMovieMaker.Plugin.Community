using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputMapComposite
{
    public enum OutputMapCompositeMapType
    {
        [Display(Name = nameof(Texts.OutputMapCompositeMapTypeLuminance), Description = nameof(Texts.OutputMapCompositeMapTypeLuminanceDesc), ResourceType = typeof(Texts))]
        Luminance = 0,

        [Display(Name = nameof(Texts.OutputMapCompositeMapTypeAlpha), Description = nameof(Texts.OutputMapCompositeMapTypeAlphaDesc), ResourceType = typeof(Texts))]
        Alpha = 1,
    }
}
