using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    public enum ShapeDisplayMode
    {
        [Display(Name = nameof(Texts.ShapePasteEffectEnum_DisplayMode_Overlay), Description = nameof(Texts.ShapePasteEffectEnum_DisplayMode_Discription_Overlay), ResourceType = typeof(Texts))]
        Overlay,

        [Display(Name = nameof(Texts.ShapePasteEffectEnum_DisplayMode_Replace), Description = nameof(Texts.ShapePasteEffectEnum_DisplayMode_Discription_Replace), ResourceType = typeof(Texts))]
        Replace,

        [Display(Name = nameof(Texts.ShapePasteEffectEnum_DisplayMode_InsideArea), Description = nameof(Texts.ShapePasteEffectEnum_DisplayMode_Discription_InsideArea), ResourceType = typeof(Texts))]
        InsideArea,

        [Display(Name = nameof(Texts.ShapePasteEffectEnum_DisplayMode_AboveArea), Description = nameof(Texts.ShapePasteEffectEnum_DisplayMode_Discription_AboveArea), ResourceType = typeof(Texts))]
        AboveArea,
    }
}
