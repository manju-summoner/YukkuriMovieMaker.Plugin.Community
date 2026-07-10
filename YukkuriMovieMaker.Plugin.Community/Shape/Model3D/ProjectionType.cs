using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

internal enum ProjectionType
{
    [Display(Name = nameof(Texts.Projection_Parallel), ResourceType = typeof(Texts))]
    Parallel,

    [Display(Name = nameof(Texts.Projection_Perspective), ResourceType = typeof(Texts))]
    Perspective
}
