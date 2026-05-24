using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Falsetto
{
    [AudioEffect(nameof(Texts.FalsettoEffect), [AudioEffectCategories.Effect], ["falsetto", "pitch", "裏声", "うらごえ"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class FalsettoEffect : AudioEffectBase
    {
        public override string Label => $"{Texts.FalsettoEffect} {PitchSemitones.GetValue(0, 1, 30):+0.0;-0.0;0.0}st";

        [Display(GroupName = nameof(Texts.FalsettoEffect), Name = nameof(Texts.PitchName), Description = nameof(Texts.PitchDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "st", -12, 24)]
        public Animation PitchSemitones { get; } = new Animation(7, -24, 36);

        [Display(GroupName = nameof(Texts.FalsettoEffect), Name = nameof(Texts.FormantName), Description = nameof(Texts.FormantDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "st", -12, 12)]
        public Animation FormantSemitones { get; } = new Animation(0, -24, 24);

        [Display(GroupName = nameof(Texts.FalsettoEffect), Name = nameof(Texts.BreathName), Description = nameof(Texts.BreathDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Breath { get; } = new Animation(15, 0, 100);

        [Display(GroupName = nameof(Texts.FalsettoEffect), Name = nameof(Texts.MixName), Description = nameof(Texts.MixDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Mix { get; } = new Animation(100, 0, 100);

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new FalsettoEffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [PitchSemitones, FormantSemitones, Breath, Mix];
    }
}
