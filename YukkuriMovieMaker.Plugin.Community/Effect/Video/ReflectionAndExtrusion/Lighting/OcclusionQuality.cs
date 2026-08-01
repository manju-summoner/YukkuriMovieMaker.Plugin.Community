using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting
{
    internal enum OcclusionQuality
    {
        [Display(Name = nameof(Texts.OcclusionQualityLow), ResourceType = typeof(Texts))]
        Low = 4,
        [Display(Name = nameof(Texts.OcclusionQualityMedium), ResourceType = typeof(Texts))]
        Medium = 6,
        [Display(Name = nameof(Texts.OcclusionQualityHigh), ResourceType = typeof(Texts))]
        High = 8,
    }
}
