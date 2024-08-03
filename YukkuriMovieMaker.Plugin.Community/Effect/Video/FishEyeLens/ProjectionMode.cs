using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FishEyeLens
{
    enum ProjectionMode
    {
        [Display(Name = nameof(Texts.OrthographicProjection), ResourceType = typeof(Texts))]
        Orthographic = 1,
        [Display(Name = nameof(Texts.StereographicProjection), ResourceType = typeof(Texts))]
        Stereographic = 2,
        [Display(Name = nameof(Texts.EquidistantProjection), ResourceType = typeof(Texts))]
        Equidistant = 3,
        [Display(Name = nameof(Texts.EquisolidProjection), ResourceType = typeof(Texts))]
        Equisolid = 4,
    }
}
