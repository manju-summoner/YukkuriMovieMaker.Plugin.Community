using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Rotate
{
    [VideoEffect(nameof(Texts.AudioVolumeRotateEffect), [VideoEffectCategories.Animation], ["角度", "audio volume rotate", "audio volume rotation", "audio volume angle"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class AudioVolumeRotateEffect : AudioVolumeVideoEffectBase
    {
        public override string Label => $"{Texts.AudioVolumeRotateEffect} X{X.GetValue(0, 1, 30):F1}°, Y{X.GetValue(0, 1, 30):F1}°, Z{X.GetValue(0, 1, 30):F1}";

        [Display(GroupName = nameof(Texts.AudioVolumeRotateEffect), Name = nameof(Texts.X), Description = nameof(Texts.XDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360.0, 360.0)]
        public Animation X { get; } = new Animation(0.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeRotateEffect), Name = nameof(Texts.Y), Description = nameof(Texts.YDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360.0, 360.0)]
        public Animation Y { get; } = new Animation(0.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeRotateEffect), Name = nameof(Texts.Z), Description = nameof(Texts.ZDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360.0, 360.0)]
        public Animation Z { get; } = new Animation(30.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeRotateEffect), Name = nameof(Texts.Is3D), Description = nameof(Texts.Is3DDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Is3D { get => is3D; set => Set(ref is3D, value); }
        bool is3D = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AudioVolumeRotateEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Z, ..base.GetAnimatables()];
    }
}
