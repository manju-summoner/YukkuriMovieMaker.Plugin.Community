using System.ComponentModel.DataAnnotations;

namespace YukkuriMovieMaker.Plugin.Community.Transition.ReelSpin
{
    public enum ReelSpinTransitionPattern
    {
        [Display(Name = nameof(Texts.ReelSpinTransitionPatternAlternateName), Description = nameof(Texts.ReelSpinTransitionPatternAlternateName), ResourceType = typeof(Texts))]
        Alternate = 0,
        [Display(Name = nameof(Texts.ReelSpinTransitionPatternGroupedName), Description = nameof(Texts.ReelSpinTransitionPatternGroupedName), ResourceType = typeof(Texts))]
        Grouped = 1,
    }
}
