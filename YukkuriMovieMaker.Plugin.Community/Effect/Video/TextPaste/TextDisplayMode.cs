using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TextPaste.TextPaste_Enum
{
    public enum TextDisplayMode
    {
        [Display(Name = nameof(Texts.TextPasteEffectEnum_DisplayMode_Replace), Description = nameof(Texts.TextPasteEffectEnum_DisplayMode_Discription_Replace), ResourceType = typeof(Texts))]
        Replace,

        [Display(Name = nameof(Texts.TextPasteEffectEnum_DisplayMode_InsideArea), Description = nameof(Texts.TextPasteEffectEnum_DisplayMode_Discription_InsideArea), ResourceType = typeof(Texts))]
        InsideArea,

        [Display(Name = nameof(Texts.TextPasteEffectEnum_DisplayMode_AboveArea), Description = nameof(Texts.TextPasteEffectEnum_DisplayMode_Discription_AboveArea), ResourceType = typeof(Texts))]
        AboveArea,

        [Display(Name = nameof(Texts.TextPasteEffectEnum_DisplayMode_Overlay), Description = nameof(Texts.TextPasteEffectEnum_DisplayMode_Discription_Overlay), ResourceType = typeof(Texts))]
        Overlay,
    }
}
