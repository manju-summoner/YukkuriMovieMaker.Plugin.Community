using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    public enum CrossHatchLineLayers
    {
        [Display(Name = nameof(Texts.CrossHatchShadingLineLayersAuto), ResourceType = typeof(Texts))]
        Auto = 0,

        [Display(Name = nameof(Texts.CrossHatchShadingLineLayers1), ResourceType = typeof(Texts))]
        One = 1,

        [Display(Name = nameof(Texts.CrossHatchShadingLineLayers2), ResourceType = typeof(Texts))]
        Two = 2,

        [Display(Name = nameof(Texts.CrossHatchShadingLineLayers3), ResourceType = typeof(Texts))]
        Three = 3,

        [Display(Name = nameof(Texts.CrossHatchShadingLineLayers4), ResourceType = typeof(Texts))]
        Four = 4,
    }
}
