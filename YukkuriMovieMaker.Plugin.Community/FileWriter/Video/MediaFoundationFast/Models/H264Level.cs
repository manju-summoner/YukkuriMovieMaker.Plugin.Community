using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Localization;

namespace YukkuriMovieMaker.Plugin.Community.FileWriter.MediaFoundationFast.Models;

public enum H264Level
{
    [Display(Name = nameof(Texts.H264LevelAuto), ResourceType = typeof(Texts), Order = 0)]
    Auto = -1,
    [Display(Name = nameof(Texts.H264Level1), ResourceType = typeof(Texts), Order = 10)]
    Level1 = 10,
    [Display(Name = nameof(Texts.H264Level1_1), ResourceType = typeof(Texts), Order = 11)]
    Level1_1 = 11,
    [Display(Name = nameof(Texts.H264Level1_2), ResourceType = typeof(Texts), Order = 12)]
    Level1_2 = 12,
    [Display(Name = nameof(Texts.H264Level1_3), ResourceType = typeof(Texts), Order = 13)]
    Level1_3 = 13,
    [Display(Name = nameof(Texts.H264Level2), ResourceType = typeof(Texts), Order = 20)]
    Level2 = 20,
    [Display(Name = nameof(Texts.H264Level2_1), ResourceType = typeof(Texts), Order = 21)]
    Level2_1 = 21,
    [Display(Name = nameof(Texts.H264Level2_2), ResourceType = typeof(Texts), Order = 22)]
    Level2_2 = 22,
    [Display(Name = nameof(Texts.H264Level3), ResourceType = typeof(Texts), Order = 30)]
    Level3 = 30,
    [Display(Name = nameof(Texts.H264Level3_1), ResourceType = typeof(Texts), Order = 31)]
    Level3_1 = 31,
    [Display(Name = nameof(Texts.H264Level3_2), ResourceType = typeof(Texts), Order = 32)]
    Level3_2 = 32,
    [Display(Name = nameof(Texts.H264Level4), ResourceType = typeof(Texts), Order = 40)]
    Level4 = 40,
    [Display(Name = nameof(Texts.H264Level4_1), ResourceType = typeof(Texts), Order = 41)]
    Level4_1 = 41,
    [Display(Name = nameof(Texts.H264Level4_2), ResourceType = typeof(Texts), Order = 42)]
    Level4_2 = 42,
    [Display(Name = nameof(Texts.H264Level5), ResourceType = typeof(Texts), Order = 50)]
    Level5 = 50,
    [Display(Name = nameof(Texts.H264Level5_1), ResourceType = typeof(Texts), Order = 51)]
    Level5_1 = 51,
    [Display(Name = nameof(Texts.H264Level5_2), ResourceType = typeof(Texts), Order = 52)]
    Level5_2 = 52,
}
