using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Dithering
{
    public enum DitheringMode
    {
        [Display(Name = nameof(Texts.ModeRgb), Description = nameof(Texts.ModeRgbDescription), ResourceType = typeof(Texts))]
        Rgb,

        [Display(Name = nameof(Texts.ModeGrayscale), Description = nameof(Texts.ModeGrayscaleDescription), ResourceType = typeof(Texts))]
        Grayscale,

        [Display(Name = nameof(Texts.ModeDuotone), Description = nameof(Texts.ModeDuotoneDescription), ResourceType = typeof(Texts))]
        Duotone,
    }
}
