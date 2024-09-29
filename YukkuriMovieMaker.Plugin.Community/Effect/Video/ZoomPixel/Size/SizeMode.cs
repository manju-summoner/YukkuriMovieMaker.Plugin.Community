using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ZoomPixel.Size
{
    public enum SizeMode
    {
        [Display(Name = nameof(Texts.BothStretch), Description = nameof(Texts.BothStretch), ResourceType = typeof(Texts))]
        BothStretch,
        [Display(Name = nameof(Texts.WidthStretch), Description = nameof(Texts.WidthStretch), ResourceType = typeof(Texts))]
        [YMM4Only]
        WidthStretch,
        [Display(Name = nameof(Texts.HeightStretch), Description = nameof(Texts.HeightStretch), ResourceType = typeof(Texts))]
        [YMM4Only]
        HeightStretch,
        [Display(Name = nameof(Texts.BothFit), Description = nameof(Texts.BothFit), ResourceType = typeof(Texts))]
        [YMM4Only]
        BothFit,
        [Display(Name = nameof(Texts.BothFill), Description = nameof(Texts.BothFill), ResourceType = typeof(Texts))]
        [YMM4Only]
        BothFill,
    }
}
