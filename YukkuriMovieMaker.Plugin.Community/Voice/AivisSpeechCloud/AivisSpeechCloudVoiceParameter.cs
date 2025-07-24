using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud
{
    internal class AivisSpeechCloudVoiceParameter : VoiceParameterBase
    {
        double
            speed = 100, 
            emotionalIntensity = 100,
            tempoDynamics = 100,
            pitch = 0,
            leadingSilenceSeconds = 0.1,
            trailingSilenceSeconds = 0.1,
            lineBreakSilenceSeconds = 0.1;
        int style;

        [Display(Name = nameof(Texts.Speed), Description = nameof(Texts.Speed), ResourceType = typeof(Texts))]
        [Range(50d, 200d)]
        [DefaultValue(50d)]
        [TextBoxSlider("F1", "%", 50, 100)]
        public double Speed { get => speed; set => Set(ref speed, value); }

        [Display(Name = nameof(Texts.Style), Description = nameof(Texts.Style), ResourceType = typeof(Texts))]
        [AivisSpeechCloudStyleComboBox]
        public int Style { get => style; set => Set(ref style, value); }

        [Display(Name = nameof(Texts.EmotionalIntensity), Description = nameof(Texts.EmotionalIntensityDesc), ResourceType = typeof(Texts))]
        [Range(0d, 200d)]
        [DefaultValue(100d)]
        [TextBoxSlider("F1", "%", 0, 100)]
        public double EmotionalIntensity { get => emotionalIntensity; set => Set(ref emotionalIntensity, value); }

        [Display(Name = nameof(Texts.TempoDynamics), Description = nameof(Texts.TempoDynamicsDesc), ResourceType = typeof(Texts))]
        [Range(50d, 200d)]
        [DefaultValue(100d)]
        [TextBoxSlider("F1", "%", 50, 100)]
        public double TempoDynamics { get => tempoDynamics; set => Set(ref tempoDynamics, value); }

        [Display(Name = nameof(Texts.Pitch), Description = nameof(Texts.PitchDesc), ResourceType = typeof(Texts))]
        [Range(-100d,100d)]
        [DefaultValue(0d)]
        [TextBoxSlider("F1", "%", -100, 100)]
        public double Pitch { get => pitch; set => Set(ref pitch, value); }

        [Display(Name = nameof(Texts.LeadingSilenceSeconds), Description = nameof(Texts.LeadingSilenceSecondsDesc), ResourceType = typeof(Texts))]
        [Range(0d, 10d)]
        [DefaultValue(0.1d)]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0, 1, ResourceType = typeof(Texts))]
        public double LeadingSilenceSeconds { get => leadingSilenceSeconds; set => Set(ref leadingSilenceSeconds, value); }

        [Display(Name = nameof(Texts.TrailingSilenceSeconds), Description = nameof(Texts.TrailingSilenceSecondsDesc), ResourceType = typeof(Texts))]
        [Range(0d, 10d)]
        [DefaultValue(0.1d)]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0, 1, ResourceType = typeof(Texts))]
        public double TrailingSilenceSeconds { get => trailingSilenceSeconds; set => Set(ref trailingSilenceSeconds, value); }

        [Display(Name = nameof(Texts.LineBreakSilenceSeconds), Description = nameof(Texts.LineBreakSilenceSecondsDesc), ResourceType = typeof(Texts))]
        [Range(0d, 10d)]
        [DefaultValue(0.1d)]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0, 1, ResourceType = typeof(Texts))]
        public double LineBreakSilenceSeconds { get => lineBreakSilenceSeconds; set => Set(ref lineBreakSilenceSeconds, value); }
    }
}
