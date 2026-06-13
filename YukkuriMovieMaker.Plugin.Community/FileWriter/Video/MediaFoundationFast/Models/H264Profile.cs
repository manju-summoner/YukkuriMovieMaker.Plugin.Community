using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models;

public enum H264Profile
{
    [Display(Name = nameof(Texts.H264ProfileBaseline), Description = nameof(Texts.H264ProfileBaselineDesc), ResourceType = typeof(Texts))]
    Baseline = 66,
    [Display(Name = nameof(Texts.H264ProfileMain), Description = nameof(Texts.H264ProfileMainDesc), ResourceType = typeof(Texts))]
    Main = 77,
    [Display(Name = nameof(Texts.H264ProfileHigh), Description = nameof(Texts.H264ProfileHighDesc), ResourceType = typeof(Texts))]
    High = 100,
}
