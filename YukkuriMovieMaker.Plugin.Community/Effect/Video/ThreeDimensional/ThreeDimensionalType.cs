using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ThreeDimensional
{
    public enum ThreeDimensionalType
    {
        [Display(Name = nameof(Texts.ThreeDimensionalTypeImageName), Description = nameof(Texts.ThreeDimensionalTypeImageDesc), ResourceType = typeof(Texts))]
        Image = 1,
        [Display(Name = nameof(Texts.ThreeDimensionalTypeSolidName), Description = nameof(Texts.ThreeDimensionalTypeSolidDesc), ResourceType = typeof(Texts))]
        Solid = 2,
        [Display(Name = nameof(Texts.ThreeDimensionalTypeGradientName), Description = nameof(Texts.ThreeDimensionalTypeGradientDesc), ResourceType = typeof(Texts))]
        Gradient = 4,
    }
}
