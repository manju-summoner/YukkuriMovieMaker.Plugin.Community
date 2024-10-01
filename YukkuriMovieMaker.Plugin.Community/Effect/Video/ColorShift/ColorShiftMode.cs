using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorShift
{
    public enum ColorShiftMode
    {
        [Display(Name = nameof(Texts.ColorShiftModeRBGName), Description = nameof(Texts.ColorShiftModeRBGDesc), ResourceType = typeof(Texts))]
        RBG = 1,
        [Display(Name = nameof(Texts.ColorShiftModeRGBName), Description = nameof(Texts.ColorShiftModeRGBDesc), ResourceType = typeof(Texts))]
        RGB = 2,
        [Display(Name = nameof(Texts.ColorShiftModeGRBName), Description = nameof(Texts.ColorShiftModeGRBDesc), ResourceType = typeof(Texts))]
        GRB = 3
    }
}
