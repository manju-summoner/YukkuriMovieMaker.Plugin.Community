using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorTransfer
{
    [VideoEffect(nameof(Texts.ColorTransferEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagColorTransfer), nameof(Texts.TagColorMatch), nameof(Texts.TagGrading)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class ColorTransferEffect : VideoEffectBase
    {
        public override string Label => Texts.ColorTransferEffectName;

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferReference), Description = nameof(Texts.ColorTransferReferenceDescription), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ColorTransferReference Reference { get => _reference; set => Set(ref _reference, value); }
        private ColorTransferReference _reference = ColorTransferReference.Scene;

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferScene), Description = nameof(Texts.ColorTransferSceneDescription), Order = 1, ResourceType = typeof(Texts))]
        [SceneComboBox]
        [ShowPropertyEditorWhen(nameof(Reference), ColorTransferReference.Scene)]
        public Guid SceneId { get => _sceneId; set => Set(ref _sceneId, value); }
        private Guid _sceneId;

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferTimeOffset), Description = nameof(Texts.ColorTransferTimeOffsetDescription), Order = 2, ResourceType = typeof(Texts))]
        [TimeSpanRange]
        [TimeSpanDefaultValue]
        [TimeSpanEditor]
        [ShowPropertyEditorWhen(nameof(Reference), ColorTransferReference.Scene)]
        public TimeSpan TimeOffset { get => _timeOffset; set => Set(ref _timeOffset, value); }
        private TimeSpan _timeOffset = TimeSpan.Zero;

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferBranchIndex), Description = nameof(Texts.ColorTransferBranchIndexDescription), Order = 3, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 0, 16)]
        [Range(0, 1024)]
        [DefaultValue(1)]
        [ShowPropertyEditorWhen(nameof(Reference), ColorTransferReference.Branch)]
        public int BranchIndex
        {
            get => _branchIndex;
            set => Set(ref _branchIndex, Math.Clamp(value, 0, 1024));
        }
        private int _branchIndex = 1;

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferMode), Description = nameof(Texts.ColorTransferModeDescription), Order = 4, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ColorTransferMode Mode { get => _mode; set => Set(ref _mode, value); }
        private ColorTransferMode _mode = ColorTransferMode.MeanAndVariance;

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferLightnessAmount), Description = nameof(Texts.ColorTransferLightnessAmountDescription), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation LightnessAmount { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferColorAmount), Description = nameof(Texts.ColorTransferColorAmountDescription), Order = 6, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ColorAmount { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.ColorTransferEffectName), Name = nameof(Texts.ColorTransferPositionAmount), Description = nameof(Texts.ColorTransferPositionAmountDescription), Order = 7, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        [ShowPropertyEditorWhen(nameof(Reference), ColorTransferReference.Scene)]
        public Animation PositionAmount { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.ColorTransferDetailGroup), Name = nameof(Texts.ColorTransferMaximumGain), Description = nameof(Texts.ColorTransferMaximumGainDescription), Order = 10, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "", 1, 16)]
        [Range(1, 64)]
        [DefaultValue(4d)]
        [ShowPropertyEditorWhen(nameof(Mode), ColorTransferMode.MeanAndVariance | ColorTransferMode.Histogram)]
        public double MaximumGain
        {
            get => _maximumGain;
            set => Set(ref _maximumGain, Math.Clamp(value, 1d, 64d));
        }
        private double _maximumGain = 4d;

        [Display(GroupName = nameof(Texts.ColorTransferDetailGroup), Name = nameof(Texts.ColorTransferSampleSize), Description = nameof(Texts.ColorTransferSampleSizeDescription), Order = 11, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "px", 64, 512)]
        [Range(32, 512)]
        [DefaultValue(128)]
        public int SampleSize
        {
            get => _sampleSize;
            set => Set(ref _sampleSize, Math.Clamp(value, 32, 512));
        }
        private int _sampleSize = 128;

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new ColorTransferEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [LightnessAmount, ColorAmount, PositionAmount];
    }
}
