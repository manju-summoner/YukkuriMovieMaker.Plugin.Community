using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Commons.EffectBase;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AudioVolume.Skew
{
    [VideoEffect(nameof(Texts.AudioVolumeSkewEffect), [VideoEffectCategories.Animation], ["audio volume skew"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class AudioVolumeSkewEffect : AudioVolumeVideoEffectBase
    {
        public override string Label => Texts.AudioVolumeSkewEffect;

        [Display(GroupName = nameof(Texts.AudioVolumeSkewEffect), Name = nameof(Texts.AngleX), Description = nameof(Texts.AngleXDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -90.0, 90.0)]
        public Animation AngleX { get; } = new Animation(0.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeSkewEffect), Name = nameof(Texts.AngleY), Description = nameof(Texts.AngleYDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -90.0, 90.0)]
        public Animation AngleY { get; } = new Animation(0.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeSkewEffect), Name = nameof(Texts.CenterPoint), Description = nameof(Texts.CenterPointDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SkewCenterPoint CenterPoint { get => centerPoint; set => Set(ref centerPoint, value); }
        SkewCenterPoint centerPoint = SkewCenterPoint.Center;

        [Display(GroupName = nameof(Texts.AudioVolumeSkewEffect), Name = nameof(CenterX), Description = nameof(Texts.CenterXDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        [SkewCenterPointCustomVisible]
        public Animation CenterX { get; } = new Animation(0.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.AudioVolumeSkewEffect), Name = nameof(CenterY), Description = nameof(Texts.CenterYDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        [SkewCenterPointCustomVisible]
        public Animation CenterY { get; } = new Animation(0.0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AudioVolumeSkewEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [AngleX, AngleY, CenterX, CenterY, ..base.GetAnimatables()];
    }
}
