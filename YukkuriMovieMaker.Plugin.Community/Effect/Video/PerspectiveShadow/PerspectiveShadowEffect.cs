using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PerspectiveShadow
{
    [VideoEffect(
        nameof(Texts.PerspectiveShadowEffectName), [VideoEffectCategories.Decoration],
        [
            nameof(Texts.TagPerspectiveShadow),
            nameof(Texts.TagPerspective),
            nameof(Texts.TagShadow3D),
            nameof(Texts.TagCastShadow),
            nameof(Texts.TagShadow)
        ],
        IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class PerspectiveShadowEffect : VideoEffectBase
    {
        public override string Label => Texts.PerspectiveShadowEffectName;

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupLight), Name = nameof(Texts.PerspectiveShadowLightXName), Description = nameof(Texts.PerspectiveShadowLightXDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000d, 1000d)]
        public Animation LightX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupLight), Name = nameof(Texts.PerspectiveShadowLightYName), Description = nameof(Texts.PerspectiveShadowLightYDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000d, 1000d)]
        public Animation LightY { get; } = new Animation(-500, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupLight), Name = nameof(Texts.PerspectiveShadowLightHeightName), Description = nameof(Texts.PerspectiveShadowLightHeightDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 50d, 2000d)]
        public Animation LightHeight { get; } = new Animation(2000, 1d, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupLight), Name = nameof(Texts.PerspectiveShadowGroundYName), Description = nameof(Texts.PerspectiveShadowGroundYDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000d, 1000d)]
        public Animation GroundY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupAppearance), Name = nameof(Texts.PerspectiveShadowOpacityName), Description = nameof(Texts.PerspectiveShadowOpacityDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Opacity { get; } = new Animation(75, 0d, 100d);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupAppearance), Name = nameof(Texts.PerspectiveShadowFalloffName), Description = nameof(Texts.PerspectiveShadowFalloffDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Falloff { get; } = new Animation(40, 0d, 100d);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupAppearance), Name = nameof(Texts.PerspectiveShadowBlurName), Description = nameof(Texts.PerspectiveShadowBlurDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 32d)]
        public Animation BlurRadius { get; } = new Animation(2, 0d, 256d);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupAppearance), Name = nameof(Texts.PerspectiveShadowSpreadName), Description = nameof(Texts.PerspectiveShadowSpreadDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 1d)]
        public Animation Spread { get; } = new Animation(0.30, 0d, 1d);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupAppearance), Name = nameof(Texts.PerspectiveShadowAlphaThresholdName), Description = nameof(Texts.PerspectiveShadowAlphaThresholdDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 1d)]
        public Animation AlphaThreshold { get; } = new Animation(0.05, 0d, 1d);

        [Display(GroupName = nameof(Texts.PerspectiveShadowGroupAppearance), Name = nameof(Texts.PerspectiveShadowShadowColorName), Description = nameof(Texts.PerspectiveShadowShadowColorDesc), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color ShadowColor
        {
            get => _shadowColor;
            set => Set(ref _shadowColor, value);
        }
        private Color _shadowColor = Color.FromArgb(255, 0, 0, 0);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new PerspectiveShadowEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            LightX,
            LightY,
            LightHeight,
            GroundY,
            Opacity,
            Falloff,
            BlurRadius,
            Spread,
            AlphaThreshold,
        ];
    }
}
