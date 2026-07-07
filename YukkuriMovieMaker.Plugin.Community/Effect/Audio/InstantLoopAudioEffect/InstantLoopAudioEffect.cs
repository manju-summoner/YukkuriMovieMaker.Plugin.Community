using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.InstantLoopAudioEffect
{
    [AudioEffect(nameof(Texts.InstantLoopAudioEffect), [AudioEffectCategories.Effect], ["loop", "stutter", "instant loop", nameof(Texts.TagLoop), nameof(Texts.TagStutter)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class InstantLoopAudioEffect : AudioEffectBase
    {
        public override string Label => Texts.InstantLoopAudioEffect;

        [Display(GroupName = nameof(Texts.InstantLoopAudioEffect), Name = nameof(Texts.StartOffsetName), Description = nameof(Texts.StartOffsetDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "ms", 0, 10000)]
        public Animation StartOffset { get; } = new Animation(0, 0, 100000);

        [Display(GroupName = nameof(Texts.InstantLoopAudioEffect), Name = nameof(Texts.IntervalName), Description = nameof(Texts.IntervalDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "ms", 1, 10000)]
        public Animation Interval { get; } = new Animation(500, 1, 100000);

        [Display(GroupName = nameof(Texts.InstantLoopAudioEffect), Name = nameof(Texts.GapName), Description = nameof(Texts.GapDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "ms", 0, 10000)]
        public Animation Gap { get; } = new Animation(0, 0, 100000);

        [Display(GroupName = nameof(Texts.InstantLoopAudioEffect), Name = nameof(Texts.RepeatCountName), Description = nameof(Texts.RepeatCountDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", nameof(Texts.UnitCount), 0, 100, ResourceType = typeof(Texts))]
        public Animation RepeatCount { get; } = new Animation(0, 0, 10000);

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
            => new InstantLoopAudioEffectProcessor(this);

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
            => [];

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [StartOffset, Interval, Gap, RepeatCount];
    }
}