using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsVoiceParameter : VoiceParameterBase
    {
        double
            speed = 100,
            stability = 50,
            similarityBoost = 75, 
            style = 0;
        bool useSpeakerBoost = false;

        [Display(Name = nameof(Texts.SpeedName), Description = nameof(Texts.SpeedDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 70,120)]
        [Range(70d, 120d)]
        [DefaultValue(100d)]
        public double Speed { get => speed; set => Set(ref speed, value); }

        [Display(Name = nameof(Texts.StabilityName), Description = nameof(Texts.StabilityDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(50d)]
        public double Stability { get => stability; set => Set(ref stability, value); }

        [Display(Name = nameof(Texts.SimilarityBoostName), Description = nameof(Texts.SimilarityBoostDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(75d)]
        public double SimilarityBoost { get => similarityBoost; set => Set(ref similarityBoost, value); }

        [Display(Name = nameof(Texts.StyleName), Description = nameof(Texts.StyleDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(0d)]
        public double Style { get => style; set => Set(ref style, value); }

        [Display(Name = nameof(Texts.UseSpeakerBoostName), Description = nameof(Texts.UseSpeakerBoostDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool UseSpeakerBoost { get => useSpeakerBoost; set => Set(ref useSpeakerBoost, value); }

        public object CreateVoiceSettings()
        {
            return new
            {
                stability = Stability / 100,
                similarity_boost = SimilarityBoost / 100,
                style = Style / 100,
                use_speaker_boost = UseSpeakerBoost,
                speed = Speed / 100,
            };
        }
    }
}