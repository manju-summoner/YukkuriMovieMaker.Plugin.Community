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

        [Display(Name = "32Hz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B32 { get; } = new Animation(0, -12, 12);

        [Display(Name = "64Hz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B64 { get; } = new Animation(0, -12, 12);

        [Display(Name = "125Hz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B125 { get; } = new Animation(0, -12, 12);

        [Display(Name = "250Hz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B250 { get; } = new Animation(0, -12, 12);

        [Display(Name = "500Hz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B500 { get; } = new Animation(0, -12, 12);

        [Display(Name = "1kHz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B1k { get; } = new Animation(0, -12, 12);

        [Display(Name = "2kHz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B2k { get; } = new Animation(0, -12, 12);

        [Display(Name = "4kHz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B4k { get; } = new Animation(0, -12, 12);

        [Display(Name = "8kHz")]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation B8k { get; } = new Animation(0, -12, 12);

        [Display(Name = "16kHz")]
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
