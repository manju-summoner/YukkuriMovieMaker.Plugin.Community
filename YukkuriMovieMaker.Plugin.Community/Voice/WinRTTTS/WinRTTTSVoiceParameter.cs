using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Windows.Media.SpeechSynthesis;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.WinRTTTS
{
    internal class WinRTTTSVoiceParameter : VoiceParameterBase
    {
        double speakingRate = 1.0;
        double volume = 1.0;
        double pitch = 1.0;
        SpeechAppendedSilence appendedSilence = SpeechAppendedSilence.Default;
        SpeechPunctuationSilence punctuationSilence = SpeechPunctuationSilence.Default;

        [Display(Name = nameof(Texts.SpeakingRate), Description = nameof(Texts.SpeakingRateDescription), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "x", 0.5, 6.0, Delay = -1)]
        [DefaultValue(1.0)]
        [Range(0.5, 6.0)]
        public double SpeakingRate { get => speakingRate; set => Set(ref speakingRate, value); }

        [Display(Name = nameof(Texts.Volume), Description = nameof(Texts.VolumeDescription), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", 0.0, 1.0, Delay = -1)]
        [DefaultValue(1.0)]
        [Range(0.0, 1.0)]
        public double Volume { get => volume; set => Set(ref volume, value); }

        [Display(Name = nameof(Texts.Pitch), Description = nameof(Texts.PitchDescription), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", "", 0.0, 2.0, Delay = -1)]
        [DefaultValue(1.0)]
        [Range(0.0, 2.0)]
        public double Pitch { get => pitch; set => Set(ref pitch, value); }

        [Display(Name = nameof(Texts.AppendedSilence), Description = nameof(Texts.AppendedSilenceDescription), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpeechAppendedSilence AppendedSilence { get => appendedSilence; set => Set(ref appendedSilence, value); }

        [Display(Name = nameof(Texts.PunctuationSilence), Description = nameof(Texts.PunctuationSilenceDescription), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpeechPunctuationSilence PunctuationSilence { get => punctuationSilence; set => Set(ref punctuationSilence, value); }
    }
}
