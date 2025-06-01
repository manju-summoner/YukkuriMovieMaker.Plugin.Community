using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Move
{
    [VideoEffect(nameof(Texts.AudioVolumeMoveEffect), [VideoEffectCategories.Animation], ["描画位置", "座標", "audio volume move"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class AudioVolumeMoveEffect : AudioVolumeVideoEffectBase
    {
        public override string Label => $"{Texts.AudioVolumeMoveEffect} X{X.GetValue(0, 1, 30):F0}px, Y{Y.GetValue(0, 1, 30):F0}px, Z{Z.GetValue(0, 1, 30):F0}px";

        [Display(GroupName = nameof(Texts.AudioVolumeMoveEffect), Name = nameof(Texts.X), Description = nameof(Texts.XDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeMoveEffect), Name = nameof(Texts.Y), Description = nameof(Texts.YDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(-100, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeMoveEffect), Name = nameof(Texts.Z), Description = nameof(Texts.ZDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AudioVolumeMoveEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Z, ..base.GetAnimatables()];
    }
}
