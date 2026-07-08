using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Caustics
{
    [VideoEffect(nameof(Texts.CausticsEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagCaustics), nameof(Texts.TagWater), nameof(Texts.TagLight)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class CausticsEffect : VideoEffectBase
    {
        public override string Label => Texts.CausticsEffectName;

        [Display(GroupName = nameof(Texts.CausticsEffectName), Name = nameof(Texts.CausticsStrength), Description = nameof(Texts.CausticsStrengthDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(50, 0, 500);

        [Display(GroupName = nameof(Texts.CausticsEffectName), Name = nameof(Texts.CausticsDisplacement), Description = nameof(Texts.CausticsDisplacementDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 50)]
        public Animation Displacement { get; } = new Animation(8, 0, 500);

        [Display(GroupName = nameof(Texts.CausticsEffectName), Name = nameof(Texts.CausticsScale), Description = nameof(Texts.CausticsScaleDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 10, 500)]
        public Animation Scale { get; } = new Animation(100, 1, 2000);

        [Display(GroupName = nameof(Texts.CausticsEffectName), Name = nameof(Texts.CausticsSpeed), Description = nameof(Texts.CausticsSpeedDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -200, 200)]
        public Animation Speed { get; } = new Animation(50, -1000, 1000);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsLightColor), Description = nameof(Texts.CausticsLightColorDescription), Order = 10, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color LightColor
        {
            get => _lightColor;
            set => Set(ref _lightColor, value);
        }
        private Color _lightColor = Colors.White;

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsSharpness), Description = nameof(Texts.CausticsSharpnessDescription), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Sharpness { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsFocus), Description = nameof(Texts.CausticsFocusDescription), Order = 12, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 200)]
        public Animation Focus { get; } = new Animation(100, 0, 400);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsDispersion), Description = nameof(Texts.CausticsDispersionDescription), Order = 13, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Dispersion { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsAbsorption), Description = nameof(Texts.CausticsAbsorptionDescription), Order = 14, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Absorption { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsAbsorptionColor), Description = nameof(Texts.CausticsAbsorptionColorDescription), Order = 15, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color AbsorptionColor
        {
            get => _absorptionColor;
            set => Set(ref _absorptionColor, value);
        }
        private Color _absorptionColor = Color.FromRgb(0x2E, 0x8B, 0x9A);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsFlowSpeed), Description = nameof(Texts.CausticsFlowSpeedDescription), Order = 16, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -200, 200)]
        public Animation FlowSpeed { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsFlowAngle), Description = nameof(Texts.CausticsFlowAngleDescription), Order = 17, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation FlowAngle { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsAnisotropy), Description = nameof(Texts.CausticsAnisotropyDescription), Order = 18, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 95)]
        public Animation Anisotropy { get; } = new Animation(0, 0, 95);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsWaveAngle), Description = nameof(Texts.CausticsWaveAngleDescription), Order = 19, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation WaveAngle { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsLightOnly), Description = nameof(Texts.CausticsLightOnlyDescription), Order = 20, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool LightOnly
        {
            get => _lightOnly;
            set => Set(ref _lightOnly, value);
        }
        private bool _lightOnly = false;

        [Display(GroupName = nameof(Texts.CausticsDetailGroup), Name = nameof(Texts.CausticsSeed), Description = nameof(Texts.CausticsSeedDescription), Order = 21, ResourceType = typeof(Texts))]
        [Range(0, int.MaxValue)]
        [DefaultValue(0)]
        [TextBoxSlider("F0", "", 0, 10000)]
        public int Seed
        {
            get => _seed;
            set => Set(ref _seed, Math.Max(value, 0));
        }
        private int _seed;

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new CausticsEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Strength, Displacement, Scale, Speed, Sharpness, Focus, Dispersion, Absorption, FlowSpeed, FlowAngle, Anisotropy, WaveAngle];
    }
}
