using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models;

public enum AudioBitRate
{
    [Display(Name = nameof(Texts.AudioBitRate192), ResourceType = typeof(Texts))]
    kb192 = 192,
    [Display(Name = nameof(Texts.AudioBitRate160), ResourceType = typeof(Texts))]
    kb160 = 160,
    [Display(Name = nameof(Texts.AudioBitRate128), ResourceType = typeof(Texts))]
    kb128 = 128,
    [Display(Name = nameof(Texts.AudioBitRate96), ResourceType = typeof(Texts))]
    kb96 = 96,
}
