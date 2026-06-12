using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    public enum DirectionalColorKeyScaleMode
    {
        [Display(Name = nameof(Texts.DirectionalColorKeyScaleModePhysicalName), Description = nameof(Texts.DirectionalColorKeyScaleModePhysicalDesc), ResourceType = typeof(Texts))]
        Physical = 1,

        [Display(Name = nameof(Texts.DirectionalColorKeyScaleModeOpaqueName), Description = nameof(Texts.DirectionalColorKeyScaleModeOpaqueDesc), ResourceType = typeof(Texts))]
        Opaque = 2,

        [Display(Name = nameof(Texts.DirectionalColorKeyScaleModeForegroundName), Description = nameof(Texts.DirectionalColorKeyScaleModeForegroundDesc), ResourceType = typeof(Texts))]
        Foreground = 4,
    }
}
