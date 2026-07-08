using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Fog
{
    [VideoEffect(nameof(Texts.FogEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagFog), nameof(Texts.TagMist), nameof(Texts.TagWeather)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class FogEffect : VideoEffectBase
    {
        public override string Label => Texts.FogEffectName;

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogDensity), Description = nameof(Texts.FogDensityDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Density { get; } = new Animation(50, 0, 500);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogScale), Description = nameof(Texts.FogScaleDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 10, 500)]
        public Animation Scale { get; } = new Animation(100, 1, 2000);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogUnevenness), Description = nameof(Texts.FogUnevennessDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Unevenness { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogGradient), Description = nameof(Texts.FogGradientDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation Gradient { get; } = new Animation(0, -100, 100);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogFlowSpeed), Description = nameof(Texts.FogFlowSpeedDescription), Order = 4, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -200, 200)]
        public Animation FlowSpeed { get; } = new Animation(30, -1000, 1000);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogFlowAngle), Description = nameof(Texts.FogFlowAngleDescription), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation FlowAngle { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogChangeSpeed), Description = nameof(Texts.FogChangeSpeedDescription), Order = 6, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -200, 200)]
        public Animation ChangeSpeed { get; } = new Animation(20, -1000, 1000);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogColor), Description = nameof(Texts.FogColorDescription), Order = 7, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color FogColor
        {
            get => _fogColor;
            set => Set(ref _fogColor, value);
        }
        private Color _fogColor = Color.FromRgb(0xDC, 0xE1, 0xE6);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogSunIntensity), Description = nameof(Texts.FogSunIntensityDescription), Order = 8, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation SunIntensity { get; } = new Animation(0, 0, 500);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogSunAngle), Description = nameof(Texts.FogSunAngleDescription), Order = 9, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation SunAngle { get; } = new Animation(270, -36000, 36000);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogSunColor), Description = nameof(Texts.FogSunColorDescription), Order = 10, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color SunColor
        {
            get => _sunColor;
            set => Set(ref _sunColor, value);
        }
        private Color _sunColor = Color.FromRgb(0xFF, 0xF2, 0xD8);

        [Display(GroupName = nameof(Texts.FogEffectName), Name = nameof(Texts.FogSeed), Description = nameof(Texts.FogSeedDescription), Order = 11, ResourceType = typeof(Texts))]
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
            => new FogEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Density, Scale, Unevenness, Gradient, FlowSpeed, FlowAngle, ChangeSpeed, SunIntensity, SunAngle];
    }
}
