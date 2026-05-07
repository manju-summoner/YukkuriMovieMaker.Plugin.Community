using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.HeatHaze
{
    [VideoEffect(nameof(Texts.HeatHazeEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagHeatHaze), nameof(Texts.TagDistortion), nameof(Texts.TagHeatShimmer)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class HeatHazeEffect : VideoEffectBase
    {
        public override string Label => Texts.HeatHazeEffectName;

        [Display(GroupName = nameof(Texts.HeatHazeEffectName), Name = nameof(Texts.HeatHazeControlModeName), Description = nameof(Texts.HeatHazeControlModeDesc), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public HeatHazeControlMode ControlMode
        {
            get => _controlMode;
            set => Set(ref _controlMode, value);
        }
        private HeatHazeControlMode _controlMode = HeatHazeControlMode.Automatic;

        [Display(GroupName = nameof(Texts.HeatHazeAutoSettingsGroup), Name = nameof(Texts.HeatHazeTemperatureName), Description = nameof(Texts.HeatHazeTemperatureDesc), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "℃", -10d, 50d)]
        [AutoSettingsVisible]
        public Animation Temperature { get; } = new Animation(35, -10, 50);

        [Display(GroupName = nameof(Texts.HeatHazeAutoSettingsGroup), Name = nameof(Texts.HeatHazeHumidityName), Description = nameof(Texts.HeatHazeHumidityDesc), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        [AutoSettingsVisible]
        public Animation Humidity { get; } = new Animation(60, 0, 100);

        [Display(GroupName = nameof(Texts.HeatHazeManualSettingsGroup), Name = nameof(Texts.HeatHazeStrengthName), Description = nameof(Texts.HeatHazeStrengthDesc), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        [ManualSettingsVisible]
        public Animation Strength { get; } = new Animation(50, 0, 500);

        [Display(GroupName = nameof(Texts.HeatHazeManualSettingsGroup), Name = nameof(Texts.HeatHazeScaleName), Description = nameof(Texts.HeatHazeScaleDesc), Order = 21, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 1d, 500d)]
        [ManualSettingsVisible]
        public Animation Scale { get; } = new Animation(100, 1, 2000);

        [Display(GroupName = nameof(Texts.HeatHazeManualSettingsGroup), Name = nameof(Texts.HeatHazeFlowSpeedName), Description = nameof(Texts.HeatHazeFlowSpeedDesc), Order = 22, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", -100d, 100d)]
        [ManualSettingsVisible]
        public Animation FlowSpeed { get; } = new Animation(10, -1000, 1000);

        [Display(GroupName = nameof(Texts.HeatHazeManualSettingsGroup), Name = nameof(Texts.HeatHazeBoilSpeedName), Description = nameof(Texts.HeatHazeBoilSpeedDesc), Order = 23, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", 0d, 100d)]
        [ManualSettingsVisible]
        public Animation BoilSpeed { get; } = new Animation(10, 0, 1000);

        [Display(GroupName = nameof(Texts.HeatHazeCommonSettingsGroup), Name = nameof(Texts.HeatHazeAngleName), Description = nameof(Texts.HeatHazeAngleDesc), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0d, 360d)]
        public Animation Angle { get; } = new Animation(90, 0, 360);

        [Display(GroupName = nameof(Texts.HeatHazeCommonSettingsGroup), Name = nameof(Texts.HeatHazeChromaticAberrationName), Description = nameof(Texts.HeatHazeChromaticAberrationDesc), Order = 31, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 10d)]
        public Animation ChromaticAberration { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.HeatHazeCommonSettingsGroup), Name = nameof(Texts.HeatHazeEnableBlurName), Description = nameof(Texts.HeatHazeEnableBlurDesc), Order = 32, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool EnableBlur
        {
            get => _enableBlur;
            set => Set(ref _enableBlur, value);
        }
        private bool _enableBlur = false;

        [Display(GroupName = nameof(Texts.HeatHazeCommonSettingsGroup), Name = nameof(Texts.HeatHazeBlurStrengthName), Description = nameof(Texts.HeatHazeBlurStrengthDesc), Order = 33, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        [BlurStrengthVisible]
        public Animation BlurStrength { get; } = new Animation(50, 0, 100);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new HeatHazeEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [Temperature, Humidity, Strength, Scale, FlowSpeed, BoilSpeed, Angle, ChromaticAberration, BlurStrength];
    }
}
