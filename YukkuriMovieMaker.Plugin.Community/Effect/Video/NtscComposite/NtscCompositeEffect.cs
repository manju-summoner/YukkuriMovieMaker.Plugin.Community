using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.NtscComposite
{
    /// <summary>
    /// NTSCコンポジット信号シミュレーションエフェクト。
    /// 映像を一度コンポジット信号(Y+変調色信号)へエンコードしてから復調することで、
    /// クロスカラー・ドットクロール・色にじみを信号処理レベルで再現する。
    /// </summary>
    [VideoEffect(nameof(Texts.NtscCompositeEffectName), [VideoEffectCategories.Filtering], ["ntsc", "コンポジット", "composite", "アナログ", "analog", "レトロ", "retro", "vhs", "crt", "ブラウン管"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    class NtscCompositeEffect : VideoEffectBase
    {
        public override string Label
            => IsVhsMode ? $"{Texts.NtscCompositeEffectName} (VHS)" : Texts.NtscCompositeEffectName;

        [Display(GroupName = nameof(Texts.NtscCompositeEffectName), Name = nameof(Texts.NtscCompositeEffectScanlineCountName), Description = nameof(Texts.NtscCompositeEffectScanlineCountDesc), Order = 10, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public NtscScanlineCount ScanlineCount { get => scanlineCount; set => Set(ref scanlineCount, value); }
        NtscScanlineCount scanlineCount = NtscScanlineCount.Lines480;

        [Display(GroupName = nameof(Texts.NtscCompositeEffectName), Name = nameof(Texts.NtscCompositeEffectYCSeparationName), Description = nameof(Texts.NtscCompositeEffectYCSeparationDesc), Order = 20, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public NtscYCSeparationMode YCSeparation { get => ycSeparation; set => Set(ref ycSeparation, value); }
        NtscYCSeparationMode ycSeparation = NtscYCSeparationMode.Notch;

        [Display(GroupName = nameof(Texts.NtscCompositeEffectName), Name = nameof(Texts.NtscCompositeEffectColorBleedName), Description = nameof(Texts.NtscCompositeEffectColorBleedDesc), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 200)]
        public Animation ColorBleed { get; } = new Animation(100, 0, 200);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectName), Name = nameof(Texts.NtscCompositeEffectSharpnessName), Description = nameof(Texts.NtscCompositeEffectSharpnessDesc), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 200)]
        public Animation Sharpness { get; } = new Animation(100, 0, 200);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectName), Name = nameof(Texts.NtscCompositeEffectNoiseName), Description = nameof(Texts.NtscCompositeEffectNoiseDesc), Order = 50, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Noise { get; } = new Animation(10, 0, 100);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectVhsGroupName), Name = nameof(Texts.NtscCompositeEffectIsVhsModeName), Description = nameof(Texts.NtscCompositeEffectIsVhsModeDesc), Order = 60, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsVhsMode { get => isVhsMode; set => Set(ref isVhsMode, value); }
        bool isVhsMode = false;

        [Display(GroupName = nameof(Texts.NtscCompositeEffectVhsGroupName), Name = nameof(Texts.NtscCompositeEffectVhsTapeName), Description = nameof(Texts.NtscCompositeEffectVhsTapeDesc), Order = 70, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation VhsTapeDegradation { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectVhsGroupName), Name = nameof(Texts.NtscCompositeEffectVhsTrackingName), Description = nameof(Texts.NtscCompositeEffectVhsTrackingDesc), Order = 72, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation VhsTracking { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectVhsGroupName), Name = nameof(Texts.NtscCompositeEffectVhsNoiseName), Description = nameof(Texts.NtscCompositeEffectVhsNoiseDesc), Order = 74, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation VhsNoise { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectVhsGroupName), Name = nameof(Texts.NtscCompositeEffectVhsDropoutName), Description = nameof(Texts.NtscCompositeEffectVhsDropoutDesc), Order = 76, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation VhsDropout { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.NtscCompositeEffectName), Name = nameof(Texts.NtscCompositeEffectSetupLevelName), Description = nameof(Texts.NtscCompositeEffectSetupLevelDesc), Order = 80, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public NtscSetupLevel SetupLevel { get => setupLevel; set => Set(ref setupLevel, value); }
        NtscSetupLevel setupLevel = NtscSetupLevel.Ire0;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するフィルタが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new NtscCompositeEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [ColorBleed, Sharpness, Noise, VhsTapeDegradation, VhsTracking, VhsNoise, VhsDropout];
    }
}
