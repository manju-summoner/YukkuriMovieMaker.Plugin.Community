using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    public enum FilterType
    {
        [Display(Name = nameof(Texts.LowPass), ResourceType = typeof(Texts))]
        LowPass,
        [Display(Name = nameof(Texts.HighPass), ResourceType = typeof(Texts))]
        HighPass,
        [Display(Name = nameof(Texts.BandPass), ResourceType = typeof(Texts))]
        BandPass,
        [Display(Name = nameof(Texts.AllPass), ResourceType = typeof(Texts))]
        AllPass,
        [Display(Name = nameof(Texts.Notch), ResourceType = typeof(Texts))]
        Notch,
        [Display(Name = nameof(Texts.LowShelf), ResourceType = typeof(Texts))]
        LowShelf,
        [Display(Name = nameof(Texts.HighShelf), ResourceType = typeof(Texts))]
        HighShelf,
        [Display(Name = nameof(Texts.PeakingEQ), ResourceType = typeof(Texts))]
        PeakingEQ,
    }
}
