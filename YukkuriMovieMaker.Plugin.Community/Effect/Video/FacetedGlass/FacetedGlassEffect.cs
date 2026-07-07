using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FacetedGlass
{
    [VideoEffect(nameof(Texts.FacetedGlass), [VideoEffectCategories.Filtering, VideoEffectCategories.Decoration], [nameof(Texts.TagGlass), nameof(Texts.TagPrism), nameof(Texts.TagRefraction), nameof(Texts.TagFacet)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class FacetedGlassEffect : VideoEffectBase
    {
        public override string Label => Texts.FacetedGlass;

        [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Amount { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.GeometryGroup), Name = nameof(Texts.CellSize), Description = nameof(Texts.CellSizeDescription), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 8, 300)]
        public Animation CellSize { get; } = new Animation(72, 8, 1000);

        [Display(GroupName = nameof(Texts.GeometryGroup), Name = nameof(Texts.Irregularity), Description = nameof(Texts.IrregularityDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Irregularity { get; } = new Animation(65, 0, 100);

        [Display(GroupName = nameof(Texts.GeometryGroup), Name = nameof(Texts.Relief), Description = nameof(Texts.ReliefDescription), Order = 12, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Relief { get; } = new Animation(55, 0, 200);

        [Display(GroupName = nameof(Texts.GeometryGroup), Name = nameof(Texts.Rotation), Description = nameof(Texts.RotationDescription), Order = 13, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation Rotation { get; } = new Animation(0, -180, 180);

        [Display(GroupName = nameof(Texts.GeometryGroup), Name = nameof(Texts.Evolution), Description = nameof(Texts.EvolutionDescription), Order = 14, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Evolution { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.GeometryGroup), Name = nameof(Texts.Seed), Description = nameof(Texts.SeedDescription), Order = 15, ResourceType = typeof(Texts))]
        [Range(0, int.MaxValue)]
        [DefaultValue(0)]
        [TextBoxSlider("F0", "", 0, 10000)]
        public int Seed
        {
            get => _seed;
            set => Set(ref _seed, Math.Max(value, 0));
        }
        private int _seed;

        [Display(GroupName = nameof(Texts.OpticsGroup), Name = nameof(Texts.Refraction), Description = nameof(Texts.RefractionDescription), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation Refraction { get; } = new Animation(18, 0, 512);

        [Display(GroupName = nameof(Texts.OpticsGroup), Name = nameof(Texts.RefractiveIndex), Description = nameof(Texts.RefractiveIndexDescription), Order = 21, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 1, 2.5)]
        public Animation RefractiveIndex { get; } = new Animation(1.5, 1, 2.5);

        [Display(GroupName = nameof(Texts.OpticsGroup), Name = nameof(Texts.Dispersion), Description = nameof(Texts.DispersionDescription), Order = 22, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Dispersion { get; } = new Animation(35, 0, 100);

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Reflection), Description = nameof(Texts.ReflectionDescription), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Reflection { get; } = new Animation(55, 0, 200);

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Glint), Description = nameof(Texts.GlintDescription), Order = 31, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Glint { get; } = new Animation(40, 0, 200);

        [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.BorderWidth), Description = nameof(Texts.BorderWidthDescription), Order = 32, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation BorderWidth { get; } = new Animation(1, 0, 32);

        [Display(GroupName = nameof(Texts.LightingGroup), Name = nameof(Texts.LightAngle), Description = nameof(Texts.LightAngleDescription), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation LightAngle { get; } = new Animation(-35, -180, 180);

        [Display(GroupName = nameof(Texts.LightingGroup), Name = nameof(Texts.LightElevation), Description = nameof(Texts.LightElevationDescription), Order = 41, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 1, 89)]
        public Animation LightElevation { get; } = new Animation(45, 1, 89);

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex,
            ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new FacetedGlassEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Amount, CellSize, Irregularity, Relief, Rotation, Evolution, Refraction, RefractiveIndex, Dispersion, Reflection, Glint, BorderWidth, LightAngle, LightElevation];
    }
}
