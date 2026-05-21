using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeGlow
{
    [VideoEffect(nameof(Texts.EdgeGlowEffectName), [VideoEffectCategories.Decoration], ["edge glow", "rim light", "outline", "bloom", "エッジ発光", "リムライト", "輪郭", "ブルーム"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class EdgeGlowEffect : VideoEffectBase
    {
        public override string Label => Texts.EdgeGlowEffectName;

        [Display(GroupName = nameof(Texts.EdgeGlowGroupDetection), Name = nameof(Texts.EdgeGlowThresholdName), Description = nameof(Texts.EdgeGlowThresholdDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 1d)]
        public Animation Threshold { get; } = new Animation(0.10, 0, 1);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupDetection), Name = nameof(Texts.EdgeGlowSoftnessName), Description = nameof(Texts.EdgeGlowSoftnessDesc), Order = 101, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 1d)]
        public Animation Softness { get; } = new Animation(0.30, 0, 1);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupDetection), Name = nameof(Texts.EdgeGlowThicknessName), Description = nameof(Texts.EdgeGlowThicknessDesc), Order = 102, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0.5d, 10d)]
        public Animation Thickness { get; } = new Animation(1.0, 0.1, 64);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupDetection), Name = nameof(Texts.EdgeGlowIncludeAlphaName), Description = nameof(Texts.EdgeGlowIncludeAlphaDesc), Order = 103, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IncludeAlpha
        {
            get => _includeAlpha;
            set => Set(ref _includeAlpha, value);
        }
        private bool _includeAlpha = true;

        [Display(GroupName = nameof(Texts.EdgeGlowGroupAppearance), Name = nameof(Texts.EdgeGlowIntensityName), Description = nameof(Texts.EdgeGlowIntensityDesc), Order = 200, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0d, 5d)]
        public Animation Intensity { get; } = new Animation(1.50, 0, 20);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupAppearance), Name = nameof(Texts.EdgeGlowColorSourceName), Description = nameof(Texts.EdgeGlowColorSourceDesc), Order = 201, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EdgeGlowColorSource ColorSource
        {
            get => _colorSource;
            set => Set(ref _colorSource, value);
        }
        private EdgeGlowColorSource _colorSource = EdgeGlowColorSource.Fixed;

        [Display(GroupName = nameof(Texts.EdgeGlowGroupAppearance), Name = nameof(Texts.EdgeGlowColorName), Description = nameof(Texts.EdgeGlowColorDesc), Order = 202, ResourceType = typeof(Texts))]
        [ColorPicker]
        [FixedColorVisible]
        public Color GlowColor
        {
            get => _glowColor;
            set => Set(ref _glowColor, value);
        }
        private Color _glowColor = Color.FromArgb(255, 255, 255, 255);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupSpread), Name = nameof(Texts.EdgeGlowEnableGlowSpreadName), Description = nameof(Texts.EdgeGlowEnableGlowSpreadDesc), Order = 300, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool EnableGlowSpread
        {
            get => _enableGlowSpread;
            set => Set(ref _enableGlowSpread, value);
        }
        private bool _enableGlowSpread = true;

        [Display(GroupName = nameof(Texts.EdgeGlowGroupSpread), Name = nameof(Texts.EdgeGlowRadiusName), Description = nameof(Texts.EdgeGlowRadiusDesc), Order = 301, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 50d)]
        [GlowRadiusVisible]
        public Animation GlowRadius { get; } = new Animation(8.0, 0, 250);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupOutput), Name = nameof(Texts.EdgeGlowOpacityName), Description = nameof(Texts.EdgeGlowOpacityDesc), Order = 400, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.EdgeGlowGroupOutput), Name = nameof(Texts.EdgeGlowBlendModeName), Description = nameof(Texts.EdgeGlowBlendModeDesc), Order = 401, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Blend BlendMode
        {
            get => _blendMode;
            set => Set(ref _blendMode, value);
        }
        private Blend _blendMode = Blend.Add;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new EdgeGlowEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [Threshold, Softness, Thickness, Intensity, GlowRadius, Opacity];
    }
}
