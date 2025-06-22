using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CameraShake
{   
    [VideoEffect(nameof(Texts.CameraShakeEffect), [VideoEffectCategories.Camera], ["揺れ", "camera shake"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class CameraShakeEffect : VideoEffectBase
    {
        public override string Label => Texts.CameraShakeEffect;

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.X), Description = nameof(Texts.XDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation X { get; } = new Animation(10, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.Y), Description = nameof(Texts.YDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Y { get; } = new Animation(10, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.Z), Description = nameof(Texts.ZDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Z { get; } = new Animation(10, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.Yaw), Description = nameof(Texts.YawDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Yaw { get; } = new Animation(5, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.Pitch), Description = nameof(Texts.PitchDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Pitch { get; } = new Animation(5, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.Roll), Description = nameof(Texts.RollDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Roll { get; } = new Animation(5, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.CameraShakeEffect), Name = nameof(Texts.Span), Description = nameof(Texts.Span), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), 0, 0.25, ResourceType = typeof(Texts))]
        public Animation Span { get; } = new Animation(0.5, 0, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new CameraShakeEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Z, Pitch, Yaw, Roll, Span];
    }
}
