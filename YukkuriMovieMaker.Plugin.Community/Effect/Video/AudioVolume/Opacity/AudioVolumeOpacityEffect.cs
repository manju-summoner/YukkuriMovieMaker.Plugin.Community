using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Opacity
{
    [VideoEffect(nameof(Texts.AudioVolumeOpacityEffect), [VideoEffectCategories.Animation], ["不透明度", "audio volume opacity"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class AudioVolumeOpacityEffect : AudioVolumeVideoEffectBase
    {
        public override string Label => $"{Texts.AudioVolumeOpacityEffect} {Opacity.GetValue(0, 1, 30):F0}%";

        [Display(GroupName = nameof(Texts.AudioVolumeOpacityEffect), Name = nameof(Texts.Opacity), Description = nameof(Texts.Opacity), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.AudioVolumeOpacityEffect), Name = nameof(Texts.Invert), Description = nameof(Texts.InvertDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Invert { get => invert; set => Set(ref invert, value); }
        bool invert = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AudioVolumeOpacityEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, ..base.GetAnimatables()];
    }
}
