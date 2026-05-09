using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DynamicLerp
{
    public enum DynamicLerpWeightSource
    {
        [Display(Name = nameof(Texts.WeightSourceMapLuminance), Description = nameof(Texts.WeightSourceMapLuminanceDesc), ResourceType = typeof(Texts))]
        MapLuminance = 0,

        [Display(Name = nameof(Texts.WeightSourceMapAlpha), Description = nameof(Texts.WeightSourceMapAlphaDesc), ResourceType = typeof(Texts))]
        MapAlpha = 1,

        [Display(Name = nameof(Texts.WeightSourceTargetLuminance), Description = nameof(Texts.WeightSourceTargetLuminanceDesc), ResourceType = typeof(Texts))]
        TargetLuminance = 2,

        [Display(Name = nameof(Texts.WeightSourceTargetAlpha), Description = nameof(Texts.WeightSourceTargetAlphaDesc), ResourceType = typeof(Texts))]
        TargetAlpha = 3,

        [Display(Name = nameof(Texts.WeightSourceCurrentLuminance), Description = nameof(Texts.WeightSourceCurrentLuminanceDesc), ResourceType = typeof(Texts))]
        CurrentLuminance = 4,

        [Display(Name = nameof(Texts.WeightSourceCurrentAlpha), Description = nameof(Texts.WeightSourceCurrentAlphaDesc), ResourceType = typeof(Texts))]
        CurrentAlpha = 5,
    }
}
