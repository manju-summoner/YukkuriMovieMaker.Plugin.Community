using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeGlow
{
    public enum EdgeGlowColorSource
    {
        [Display(Name = nameof(Texts.EdgeGlowColorSourceFixed), Description = nameof(Texts.EdgeGlowColorSourceFixedDesc), ResourceType = typeof(Texts))]
        Fixed = 0,

        [Display(Name = nameof(Texts.EdgeGlowColorSourceSource), Description = nameof(Texts.EdgeGlowColorSourceSourceDesc), ResourceType = typeof(Texts))]
        Source = 1,
    }
}
