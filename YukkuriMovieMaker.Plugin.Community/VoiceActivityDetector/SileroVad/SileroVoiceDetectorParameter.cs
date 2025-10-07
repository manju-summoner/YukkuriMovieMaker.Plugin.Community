using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Transcription.VAD.SileroVad;
using YukkuriMovieMaker.Plugin.VoiceActivityDetector;

namespace YukkuriMovieMaker.Plugin.Community.VoiceActivityDetector.SileroVad
{
    public class SileroVoiceDetectorParameter : Bindable, IVoiceActivityDetectorParameter
    {
        float threshold = 50f,
            maxSpeechDurationSeconds = 30;
        int
            minSpeechDurationMs = 250,
            minSilenceDurationMs = 100,
            speechPadMs = 200;

        [Display(Name = nameof(Texts.Threshold), Description = nameof(Texts.ThresholdDesc), ResourceType = typeof(Texts))]
        [Range(0f, 100f)]
        [DefaultValue(50f)]
        [TextBoxSlider("F1", "%", 0, 100)]
        public float Threshold { get => threshold; set => Set(ref threshold, value); }

        [Display(Name = nameof(Texts.MinSpeechDurationMs), Description = nameof(Texts.MinSpeechDurationMsDesc), ResourceType = typeof(Texts))]
        [Range(0, 30_000)]
        [DefaultValue(250)]
        [TextBoxSlider("F0", nameof(Texts.MilliSecUnit), 0, 250, ResourceType = typeof(Texts))]
        public int MinSpeechDurationMs { get => minSpeechDurationMs; set => Set(ref minSpeechDurationMs, value); }

        [Display(Name = nameof(Texts.MaxSpeechDurationSeconds), Description = nameof(Texts.MaxSpeechDurationSecondsDesc), ResourceType = typeof(Texts))]
        [Range(0f, 30f)]
        [DefaultValue(30f)]
        [TextBoxSlider("F1", nameof(Texts.SecUnit), 0, 30, ResourceType = typeof(Texts))]
        public float MaxSpeechDurationSeconds { get => maxSpeechDurationSeconds; set => Set(ref maxSpeechDurationSeconds, value); }

        [Display(Name = nameof(Texts.MinSilenceDurationMs), Description = nameof(Texts.MinSilenceDurationMsDesc), ResourceType = typeof(Texts))]
        [Range(0, 30_000)]
        [DefaultValue(100)]
        [TextBoxSlider("F0", nameof(Texts.MilliSecUnit), 0, 250, ResourceType = typeof(Texts))]
        public int MinSilenceDurationMs { get => minSilenceDurationMs; set => Set(ref minSilenceDurationMs, value); }

        [Display(Name = nameof(Texts.SpeechPadMs), Description = nameof(Texts.SpeechPadMsDesc), ResourceType = typeof(Texts))]
        [Range(0, 1000)]
        [DefaultValue(200)]
        [TextBoxSlider("F0", nameof(Texts.MilliSecUnit), 0, 250, ResourceType = typeof(Texts))]
        public int SpeechPadMs { get => speechPadMs; set => Set(ref speechPadMs, value); }

        public IVoiceActivityDetector CreateDetector() => new SileroVoiceDetector(Threshold, MinSpeechDurationMs, MaxSpeechDurationSeconds, MinSilenceDurationMs, SpeechPadMs);

    }
}
