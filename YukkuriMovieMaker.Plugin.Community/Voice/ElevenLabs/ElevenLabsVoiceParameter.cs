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
        [ElevenLabsHiddenOnV4]
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
        [ElevenLabsHiddenOnV4]
        public double Style { get => style; set => Set(ref style, value); }

        [Display(Name = nameof(Texts.UseSpeakerBoostName), Description = nameof(Texts.UseSpeakerBoostDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ElevenLabsHiddenOnV4]
        public bool UseSpeakerBoost { get => useSpeakerBoost; set => Set(ref useSpeakerBoost, value); }

        public Dictionary<string, object> CreateVoiceSettings(string model)
        {
            var settings = new Dictionary<string, object>
            {
                ["stability"] = Stability / 100,
                ["similarity_boost"] = SimilarityBoost / 100,
            };
            if (!ElevenLabsTTSModels.IsV4(model))
            {
                settings["style"] = Style / 100;
                settings["use_speaker_boost"] = UseSpeakerBoost;
                settings["speed"] = Speed / 100;
            }
            return settings;
        }
    }
}