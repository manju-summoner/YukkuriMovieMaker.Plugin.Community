using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

public enum LutInterpolationMode
{
    [Display(
        Name = nameof(Texts.InterpolationTetra),
        Description = nameof(Texts.InterpolationTetraDesc),
        ResourceType = typeof(Texts))]
    Tetrahedral = 0,

    [Display(
        Name = nameof(Texts.InterpolationTrilinear),
        Description = nameof(Texts.InterpolationTrilinearDesc),
        ResourceType = typeof(Texts))]
    Trilinear = 1,
}
