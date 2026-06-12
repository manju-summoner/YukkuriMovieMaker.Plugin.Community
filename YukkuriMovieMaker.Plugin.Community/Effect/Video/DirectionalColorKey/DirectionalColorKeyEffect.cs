using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DirectionalColorKey
{
    [VideoEffect(nameof(Texts.DirectionalColorKeyEffectName), [VideoEffectCategories.Composition], ["directional color key", "dcsk", "chroma key", "方向クロマキー", "色分離キー", "変位方向キー"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class DirectionalColorKeyEffect : VideoEffectBase
    {
        public override string Label => Texts.DirectionalColorKeyEffectName;

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyBackgroundColorName), Description = nameof(Texts.DirectionalColorKeyBackgroundColorDesc), Order = 100, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color BackgroundColor { get => _backgroundColor; set => Set(ref _backgroundColor, value); }
        private Color _backgroundColor = Color.FromRgb(0, 255, 0);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyClusterCountName), Description = nameof(Texts.DirectionalColorKeyClusterCountDesc), Order = 110, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 4)]
        public Animation ClusterCount { get; } = new Animation(1, 1, 4);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyScaleModeName), Description = nameof(Texts.DirectionalColorKeyScaleModeDesc), Order = 120, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public DirectionalColorKeyScaleMode ScaleMode { get => _scaleMode; set => Set(ref _scaleMode, value); }
        private DirectionalColorKeyScaleMode _scaleMode = DirectionalColorKeyScaleMode.Physical;

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyForegroundColorName), Description = nameof(Texts.DirectionalColorKeyForegroundColorDesc), Order = 130, ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(ScaleMode), DirectionalColorKeyScaleMode.Foreground)]
        public Color ForegroundColor { get => _foregroundColor; set => Set(ref _foregroundColor, value); }
        private Color _foregroundColor = Color.FromRgb(255, 255, 255);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyOpaquePercentileName), Description = nameof(Texts.DirectionalColorKeyOpaquePercentileDesc), Order = 140, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        [ShowPropertyEditorWhen(nameof(ScaleMode), DirectionalColorKeyScaleMode.Opaque)]
        public Animation OpaquePercentile { get; } = new Animation(99, 0, 100);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyNoiseThresholdName), Description = nameof(Texts.DirectionalColorKeyNoiseThresholdDesc), Order = 150, ResourceType = typeof(Texts))]
        [AnimationSlider("F3", "", 0d, 0.2d)]
        public Animation NoiseThreshold { get; } = new Animation(0.02, 0, 1);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeySigmaColorName), Description = nameof(Texts.DirectionalColorKeySigmaColorDesc), Order = 160, ResourceType = typeof(Texts))]
        [AnimationSlider("F3", "", 0.001d, 0.5d)]
        public Animation SigmaColor { get; } = new Animation(0.1, 0.001, 1);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyEdgeSoftnessName), Description = nameof(Texts.DirectionalColorKeyEdgeSoftnessDesc), Order = 170, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation EdgeSoftness { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeySpillStrengthName), Description = nameof(Texts.DirectionalColorKeySpillStrengthDesc), Order = 180, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation SpillStrength { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyDespillBiasName), Description = nameof(Texts.DirectionalColorKeyDespillBiasDesc), Order = 190, ResourceType = typeof(Texts))]
        [AnimationSlider("F3", "", 0d, 0.5d)]
        public Animation DespillBias { get; } = new Animation(0, 0, 1);

        [Display(GroupName = nameof(Texts.DirectionalColorKeyGroupName), Name = nameof(Texts.DirectionalColorKeyOutputForegroundName), Description = nameof(Texts.DirectionalColorKeyOutputForegroundDesc), Order = 200, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool OutputForeground { get => _outputForeground; set => Set(ref _outputForeground, value); }
        private bool _outputForeground = true;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new DirectionalColorKeyEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [ClusterCount, OpaquePercentile, NoiseThreshold, SigmaColor, EdgeSoftness, SpillStrength, DespillBias];
    }
}
