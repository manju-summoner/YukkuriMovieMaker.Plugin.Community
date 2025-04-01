using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer
{
    [AudioEffect(nameof(Texts.Equalizer), [AudioEffectCategories.Filter], ["equalizer"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class EqualizerEffect : AudioEffectBase
    {
        public override string Label => Texts.Equalizer;

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B32), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B32 { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B64), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B64 { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B125), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B125 { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B250), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B250 { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B500), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B500 { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B1k), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B1k { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B2k), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B2k { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B4k), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B4k { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B8k), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B8k { get; } = new Animation(0, -12, 12);

        [Display(GroupName = nameof(Texts.Equalizer), Name = nameof(Texts.B16k), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B16k { get; } = new Animation(0, -12, 12);


        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new EqualizerEffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [B32, B64, B125, B250, B500, B1k, B2k, B4k, B8k, B16k];
    }
}
