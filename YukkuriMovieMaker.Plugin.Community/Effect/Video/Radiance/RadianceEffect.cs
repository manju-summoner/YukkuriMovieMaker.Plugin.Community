using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Radiance
{
    [VideoEffect(nameof(Texts.RadianceEffectName), [VideoEffectCategories.Decoration], [nameof(Texts.TagGlobalIllumination), nameof(Texts.TagLighting), nameof(Texts.TagGlow)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal sealed class RadianceEffect : VideoEffectBase
    {
        public override string Label => Texts.RadianceEffectName;

        [Display(GroupName = nameof(Texts.RadianceEffectName), Name = nameof(Texts.RadianceStrength), Description = nameof(Texts.RadianceStrengthDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Strength { get; } = new Animation(100, 0, 800);

        [Display(GroupName = nameof(Texts.RadianceEffectName), Name = nameof(Texts.RadianceRange), Description = nameof(Texts.RadianceRangeDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 10, 800)]
        public Animation Range { get; } = new Animation(300, 10, 2000);

        [Display(GroupName = nameof(Texts.RadianceEffectName), Name = nameof(Texts.RadianceDiffuse), Description = nameof(Texts.RadianceDiffuseDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Diffuse { get; } = new Animation(60, 0, 100);

        [Display(GroupName = nameof(Texts.RadianceEffectName), Name = nameof(Texts.RadianceAmbient), Description = nameof(Texts.RadianceAmbientDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Ambient { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.RadianceEmissionGroup), Name = nameof(Texts.RadianceThreshold), Description = nameof(Texts.RadianceThresholdDescription), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Threshold { get; } = new Animation(70, 0, 100);

        [Display(GroupName = nameof(Texts.RadianceEmissionGroup), Name = nameof(Texts.RadianceEmissionGain), Description = nameof(Texts.RadianceEmissionGainDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation EmissionGain { get; } = new Animation(150, 0, 1600);

        [Display(GroupName = nameof(Texts.RadianceEmissionGroup), Name = nameof(Texts.RadianceLightColor), Description = nameof(Texts.RadianceLightColorDescription), Order = 12, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color LightColor
        {
            get => _lightColor;
            set => Set(ref _lightColor, value);
        }
        private Color _lightColor = Colors.White;

        [Display(GroupName = nameof(Texts.RadianceEmissionGroup), Name = nameof(Texts.RadianceOcclusion), Description = nameof(Texts.RadianceOcclusionDescription), Order = 13, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Occlusion { get; } = new Animation(80, 0, 100);

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new RadianceEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Strength, Range, Diffuse, Ambient, Threshold, EmissionGain, Occlusion];
    }
}
