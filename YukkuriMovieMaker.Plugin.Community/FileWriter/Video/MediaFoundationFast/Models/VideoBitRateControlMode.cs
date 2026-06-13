using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models;

public enum VideoBitRateControlMode
{
    [Display(Name = nameof(Texts.BitRateControlModeCBR), Description = nameof(Texts.BitRateControlModeCBRDesc), ResourceType = typeof(Texts))]
    CBR = 0,
    [Display(Name = nameof(Texts.BitRateControlModeVBR), Description = nameof(Texts.BitRateControlModeVBRDesc), ResourceType = typeof(Texts))]
    UnconstrainedVBR = 2,
    [Display(Name = nameof(Texts.BitRateControlModeQuality), Description = nameof(Texts.BitRateControlModeQualityDesc), ResourceType = typeof(Texts))]
    Quality = 3,
}
