using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSVoiceParameter : VoiceParameterBase
{
    [Display(Name = nameof(Texts.NumStepsName), Description = nameof(Texts.NumStepsDesc), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "", 1, 120)]
    [Range(1, 120)]
    [DefaultValue(40)]
    public int NumSteps { get; set => Set(ref field, value); } = 40;

    [Display(Name = nameof(Texts.CfgScaleTextName), Description = nameof(Texts.CfgScaleTextDesc), ResourceType = typeof(Texts))]
    [TextBoxSlider("F1", "", 0, 10)]
    [Range(0d, 10d)]
    [DefaultValue(3d)]
    public double CfgScaleText { get; set => Set(ref field, value); } = 3d;

    [Display(Name = nameof(Texts.CfgScaleSpeakerName), Description = nameof(Texts.CfgScaleSpeakerDesc), ResourceType = typeof(Texts))]
    [TextBoxSlider("F1", "", 0, 10)]
    [Range(0d, 10d)]
    [DefaultValue(5d)]
    public double CfgScaleSpeaker { get; set => Set(ref field, value); } = 5d;

    [Display(Name = nameof(Texts.Checkpoint), Description = nameof(Texts.CheckpointDesc), ResourceType = typeof(Texts))]
    [FileSelector(FileGroupType.None)]
    [DefaultValue("")]
    public string Checkpoint { get; set => Set(ref field, value); } = string.Empty;
}
