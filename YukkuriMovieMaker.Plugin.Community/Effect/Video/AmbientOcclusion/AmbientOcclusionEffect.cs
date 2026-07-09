using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AmbientOcclusion
{
    [VideoEffect(nameof(Texts.AmbientOcclusionEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagAmbientOcclusion), nameof(Texts.TagShading), nameof(Texts.TagDepth)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class AmbientOcclusionEffect : VideoEffectBase
    {
        public override string Label => Texts.AmbientOcclusionEffectName;

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionStrength), Description = nameof(Texts.AmbientOcclusionStrengthDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionRadius), Description = nameof(Texts.AmbientOcclusionRadiusDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 4, 96)]
        public Animation Radius { get; } = new Animation(24, 1, 256);

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionHeight), Description = nameof(Texts.AmbientOcclusionHeightDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Height { get; } = new Animation(50, 0, 400);

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionTextureSuppression), Description = nameof(Texts.AmbientOcclusionTextureSuppressionDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation TextureSuppression { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionShadowColor), Description = nameof(Texts.AmbientOcclusionShadowColorDescription), Order = 4, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color ShadowColor
        {
            get => _shadowColor;
            set => Set(ref _shadowColor, value);
        }
        private Color _shadowColor = Color.FromRgb(0x1A, 0x14, 0x20);

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionDirections), Description = nameof(Texts.AmbientOcclusionDirectionsDescription), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 2, 16)]
        public Animation Directions { get; } = new Animation(8, 2, 16);

        [Display(GroupName = nameof(Texts.AmbientOcclusionEffectName), Name = nameof(Texts.AmbientOcclusionSamples), Description = nameof(Texts.AmbientOcclusionSamplesDescription), Order = 6, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 12)]
        public Animation Samples { get; } = new Animation(6, 1, 12);

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new AmbientOcclusionEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Strength, Radius, Height, TextureSuppression, Directions, Samples];
    }
}
