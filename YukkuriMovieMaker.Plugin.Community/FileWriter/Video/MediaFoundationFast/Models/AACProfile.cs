using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models;

public enum AACProfile
{
    [Display(Name = nameof(Texts.AACProfileL2), ResourceType = typeof(Texts))]
    AACL2 = 41,
    [Display(Name = nameof(Texts.AACProfileL4), ResourceType = typeof(Texts))]
    AACL4 = 42,
    [Display(Name = nameof(Texts.AACProfileL5), ResourceType = typeof(Texts))]
    AACL5 = 43,
}
