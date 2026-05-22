using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Whisper
{
    [AudioEffect(nameof(Texts.WhisperEffect), [AudioEffectCategories.Effect], ["whisper", "breath", "囁き", "ささやき"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class WhisperEffect : AudioEffectBase
    {
        public override string Label => $"{Texts.WhisperEffect} {Breathiness.GetValue(0, 1, 30):F0}%";

        [Display(GroupName = nameof(Texts.WhisperEffect), Name = nameof(Texts.BreathinessName), Description = nameof(Texts.BreathinessDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Breathiness { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.WhisperEffect), Name = nameof(Texts.HighPassName), Description = nameof(Texts.HighPassDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "Hz", 20, 800)]
        public Animation HighPassHz { get; } = new Animation(120, 20, 2000);

        [Display(GroupName = nameof(Texts.WhisperEffect), Name = nameof(Texts.BrightnessName), Description = nameof(Texts.BrightnessDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "dB", -12, 12)]
        public Animation BrightnessDb { get; } = new Animation(3, -24, 24);

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new WhisperEffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Breathiness, HighPassHz, BrightnessDb];
    }
}
