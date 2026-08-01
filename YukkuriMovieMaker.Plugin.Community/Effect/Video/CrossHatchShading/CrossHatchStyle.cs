using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    public enum CrossHatchStyle
    {
        [Display(Name = nameof(Texts.CrossHatchShadingStylePen), Description = nameof(Texts.CrossHatchShadingStylePenDesc), ResourceType = typeof(Texts))]
        Pen = 0,

        [Display(Name = nameof(Texts.CrossHatchShadingStyleEngraving), Description = nameof(Texts.CrossHatchShadingStyleEngravingDesc), ResourceType = typeof(Texts))]
        Engraving = 1,

        [Display(Name = nameof(Texts.CrossHatchShadingStyleManga), Description = nameof(Texts.CrossHatchShadingStyleMangaDesc), ResourceType = typeof(Texts))]
        Manga = 2,

        [Display(Name = nameof(Texts.CrossHatchShadingStyleBlueprint), Description = nameof(Texts.CrossHatchShadingStyleBlueprintDesc), ResourceType = typeof(Texts))]
        Blueprint = 3,
    }
}
