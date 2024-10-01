using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Wave
{
    public enum WaveDirection
    {
        [Display(Name = nameof(Texts.WaveDirectionHorizontalName), Description = nameof(Texts.WaveDirectionHorizontalDesc), ResourceType = typeof(Texts))]
        Horizontal,
        [Display(Name = nameof(Texts.WaveDirectionVerticalName), Description = nameof(Texts.WaveDirectionVerticalDesc), ResourceType = typeof(Texts))]
        Vertical,

    }
}
