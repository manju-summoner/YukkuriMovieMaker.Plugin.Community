using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Skew
{
    internal enum SkewCenterPoint
    {
        [Display(Name = nameof(Texts.SkewCenterPointCenter), Description = nameof(Texts.SkewCenterPointCenter), ResourceType = typeof(Texts))]
        Center,
        [Display(Name = nameof(Texts.SkewCenterPointTop), Description = nameof(Texts.SkewCenterPointTop), ResourceType = typeof(Texts))]
        Top,
        [Display(Name = nameof(Texts.SkewCenterPointBottom), Description = nameof(Texts.SkewCenterPointBottom), ResourceType = typeof(Texts))]
        Bottom,
        [Display(Name = nameof(Texts.SkewCenterPointLeft), Description = nameof(Texts.SkewCenterPointLeft), ResourceType = typeof(Texts))]
        Left,
        [Display(Name = nameof(Texts.SkewCenterPointRight), Description = nameof(Texts.SkewCenterPointRight), ResourceType = typeof(Texts))]
        Right,
        [Display(Name = nameof(Texts.SkewCenterPointCustom), Description = nameof(Texts.SkewCenterPointCustom), ResourceType = typeof(Texts))]
        Custom,
    }
}
