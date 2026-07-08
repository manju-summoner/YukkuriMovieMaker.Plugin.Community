using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossFilter
{
    [VideoEffect(nameof(Texts.CrossFilterEffectName), [VideoEffectCategories.Drawing], [nameof(Texts.TagCrossFilter), nameof(Texts.TagSparkle), nameof(Texts.TagLightStreak)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class CrossFilterEffect : VideoEffectBase
    {
        public override string Label => Texts.CrossFilterEffectName;

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterStrength), Description = nameof(Texts.CrossFilterStrengthDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 500)]
        public Animation Strength { get; } = new Animation(100, 0, 1000);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterThreshold), Description = nameof(Texts.CrossFilterThresholdDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Threshold { get; } = new Animation(60, 0, 100);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterLength), Description = nameof(Texts.CrossFilterLengthDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 300)]
        public Animation Length { get; } = new Animation(80, 0, 1000);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterRayCount), Description = nameof(Texts.CrossFilterRayCountDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 16)]
        public Animation RayCount { get; } = new Animation(4, 1, 16);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterAngle), Description = nameof(Texts.CrossFilterAngleDescription), Order = 4, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Angle { get; } = new Animation(45, -36000, 36000);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterThickness), Description = nameof(Texts.CrossFilterThicknessDescription), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Thickness { get; } = new Animation(1, 0, 50);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterDispersion), Description = nameof(Texts.CrossFilterDispersionDescription), Order = 6, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Dispersion { get; } = new Animation(30, 0, 100);

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterLightColor), Description = nameof(Texts.CrossFilterLightColorDescription), Order = 7, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color LightColor
        {
            get => _lightColor;
            set => Set(ref _lightColor, value);
        }
        private Color _lightColor = Colors.White;

        [Display(GroupName = nameof(Texts.CrossFilterEffectName), Name = nameof(Texts.CrossFilterLightOnly), Description = nameof(Texts.CrossFilterLightOnlyDescription), Order = 8, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool LightOnly
        {
            get => _lightOnly;
            set => Set(ref _lightOnly, value);
        }
        private bool _lightOnly = false;

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new CrossFilterEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Strength, Threshold, Length, RayCount, Angle, Thickness, Dispersion];
    }
}
