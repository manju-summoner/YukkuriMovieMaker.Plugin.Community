using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    public enum CrossHatchColorMode
    {
        [Display(Name = nameof(Texts.CrossHatchShadingColorModeInkAndPaper), Description = nameof(Texts.CrossHatchShadingColorModeInkAndPaperDesc), ResourceType = typeof(Texts))]
        InkAndPaper = 0,

        [Display(Name = nameof(Texts.CrossHatchShadingColorModePreserveSource), Description = nameof(Texts.CrossHatchShadingColorModePreserveSourceDesc), ResourceType = typeof(Texts))]
        PreserveSource = 1,
    }
}
