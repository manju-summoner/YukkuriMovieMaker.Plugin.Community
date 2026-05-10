using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    [VideoEffect(nameof(Texts.WaveClipping), [VideoEffectCategories.Composition], [nameof(Texts.TagWave), nameof(Texts.TagClipping), nameof(Texts.TagWaveKatakana)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class WaveClippingEffect : VideoEffectBase
    {
        public override string Label => Texts.WaveClipping;

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Mode), Description = nameof(Texts.ModeDescription), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public WaveClippingMode Mode
        {
            get => _mode;
            set => Set(ref _mode, value);
        }
        private WaveClippingMode _mode = WaveClippingMode.Unidirectional;

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.ClipPosition), Description = nameof(Texts.ClipPositionDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ClipPosition { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.BandWidth), Description = nameof(Texts.BandWidthDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        [BandWidthVisible]
        public Animation BandWidth { get; } = new Animation(20, 0, 100);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Amplitude), Description = nameof(Texts.AmplitudeDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 50)]
        public Animation Amplitude { get; } = new Animation(5, 0, 50);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Frequency), Description = nameof(Texts.FrequencyDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", 0, 20)]
        public Animation Frequency { get; } = new Animation(3, 0, 100);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Phase), Description = nameof(Texts.PhaseDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F3", "rad", -6.2832, 6.2832)]
        [SineModeVisible]
        public Animation Phase { get; } = new Animation(0, -314.159, 314.159);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.RandomSpeed), Description = nameof(Texts.RandomSpeedDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "", -10, 10)]
        [RandomModeVisible]
        public Animation RandomSpeed { get; } = new Animation(0, -100, 100);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Rotation), Description = nameof(Texts.RotationDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation Rotation { get; } = new Animation(0, -360, 360);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Softness), Description = nameof(Texts.SoftnessDescription), ResourceType = typeof(Texts))]
        [AnimationSlider("F4", "", 0, 0.02)]
        public Animation Softness { get; } = new Animation(0.002, 0, 1);

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.Inverted), Description = nameof(Texts.InvertedDescription), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInverted
        {
            get => _isInverted;
            set => Set(ref _isInverted, value);
        }
        private bool _isInverted;

        [Display(GroupName = nameof(Texts.WaveClipping), Name = nameof(Texts.UseRandom), Description = nameof(Texts.UseRandomDescription), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool UseRandom
        {
            get => _useRandom;
            set => Set(ref _useRandom, value);
        }
        private bool _useRandom;

        public float RandomSeed
        {
            get => _randomSeed;
            set => Set(ref _randomSeed, value);
        }
        private float _randomSeed = (float)(new Random().NextDouble() * 1000.0);

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex,
            ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new WaveClippingEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [ClipPosition, BandWidth, Amplitude, Frequency, Phase, RandomSpeed, Rotation, Softness];
    }
}
