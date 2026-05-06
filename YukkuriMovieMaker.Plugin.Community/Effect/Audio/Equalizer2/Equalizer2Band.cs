using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    public class Equalizer2Band : Animatable
    {
        FilterType filterType = FilterType.PeakingEQ;

        [Display(GroupName = nameof(Texts.Bands), Name = nameof(Texts.FilterType), Description = nameof(Texts.FilterTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public FilterType FilterType { get => filterType; set => Set(ref filterType, value); }

        [Display(GroupName = nameof(Texts.Bands), Name = nameof(Texts.Frequency), Description = nameof(Texts.FrequencyDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "Hz", 20, 20000)]
        public Animation Frequency { get; } = new Animation(1000, 20, 20000);

        [Display(GroupName = nameof(Texts.Bands), Name = nameof(Texts.Gain), Description = nameof(Texts.GainDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -24, 24)]
        public Animation Gain { get; } = new Animation(0, -24, 24);

        [Display(GroupName = nameof(Texts.Bands), Name = nameof(Texts.Q), Description = nameof(Texts.QDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0.1, 10)]
        public Animation Q { get; } = new Animation(1.41, 0.1, 10);

        public Equalizer2Band(FilterType filterType, double frequency, double gain, double q)
        {
            FilterType = filterType;
            Frequency.Values[0].Value = frequency;
            Gain.Values[0].Value = gain;
            Q.Values[0].Value = q;
        }

        public Equalizer2Band(Equalizer2Band other)
        {
            FilterType = other.FilterType;
            Frequency.CopyFrom(other.Frequency);
            Gain.CopyFrom(other.Gain);
            Q.CopyFrom(other.Q);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Frequency, Gain, Q];
    }
}
