using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusVoiceParameter : VoiceParameterBase
{
    double lengthScale = 1.0;
    double noiseScale = 0.667;
    double noiseScaleW = 0.8;

    [Display(Name = nameof(Texts.LengthScale), Description = nameof(Texts.LengthScaleDesc), ResourceType = typeof(Texts))]
    [TextBoxSlider("F2", "", 0.1, 3.0, Delay = -1)]
    [Range(0.1, 3.0)]
    [DefaultValue(1.0)]
    public double LengthScale
    {
        get => lengthScale;
        set => Set(ref lengthScale, value);
    }

    [Display(Name = nameof(Texts.NoiseScale), Description = nameof(Texts.NoiseScaleDesc), ResourceType = typeof(Texts))]
    [TextBoxSlider("F3", "", 0.0, 1.0, Delay = -1)]
    [Range(0.0, 1.0)]
    [DefaultValue(0.667)]
    public double NoiseScale
    {
        get => noiseScale;
        set => Set(ref noiseScale, value);
    }

    [Display(Name = nameof(Texts.NoiseScaleW), Description = nameof(Texts.NoiseScaleWDesc), ResourceType = typeof(Texts))]
    [TextBoxSlider("F3", "", 0.0, 1.0, Delay = -1)]
    [Range(0.0, 1.0)]
    [DefaultValue(0.8)]
    public double NoiseScaleW
    {
        get => noiseScaleW;
        set => Set(ref noiseScaleW, value);
    }
}
