using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Zoom
{
    [VideoEffect(nameof(Texts.AudioVolumeZoomEffect), [VideoEffectCategories.Animation], ["audio volume zoom"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class AudioVolumeZoomEffect : AudioVolumeVideoEffectBase
    {
        public override string Label => $"{Texts.AudioVolumeZoomEffect} X{ZoomX.GetValue(0, 1, 30) * Zoom.GetValue(0, 1, 30) / 100.0:F0}%, Y{ZoomY.GetValue(0, 1, 30) * Zoom.GetValue(0, 1, 30) / 100.0:F0}%";

        [Display(GroupName = nameof(Texts.AudioVolumeZoomEffect), Name = nameof(Texts.Zoom), Description = nameof(Texts.ZoomDesc),  ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0.0, 500.0)]
        public Animation Zoom { get; } = new Animation(150.0, 0.0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeZoomEffect), Name = nameof(Texts.ZoomX), Description = nameof(Texts.ZoomXDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0.0, 500.0)]
        public Animation ZoomX { get; } = new Animation(100.0, 0.0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeZoomEffect), Name = nameof(Texts.ZoomY), Description = nameof(Texts.ZoomY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0.0, 500.0)]
        public Animation ZoomY { get; } = new Animation(100.0, 0.0, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AudioVolumeZoomEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Zoom, ZoomX, ZoomY, ..base.GetAnimatables()];
    }
}
