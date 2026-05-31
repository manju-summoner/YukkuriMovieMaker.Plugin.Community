using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PartialOutline
{
    [VideoEffect(nameof(Texts.PartialOutlineEffectName), [VideoEffectCategories.Decoration],
        [nameof(Texts.TagPartialOutline), nameof(Texts.TagPartialBorder)],
        IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class PartialOutlineEffect : VideoEffectBase, IFileItem
    {
        public override string Label => $"{Texts.PartialOutlineEffectName} {Thickness.GetValue(0, 1, 30):F0}px";

        [Display(GroupName = nameof(Texts.PartialOutlineOutlineGroupName), Name = nameof(Texts.PartialOutlineThicknessName), Description = nameof(Texts.PartialOutlineThicknessDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 10d)]
        public Animation Thickness { get; } = new Animation(3, 0, 500);

        [Display(GroupName = nameof(Texts.PartialOutlineOutlineGroupName), Name = nameof(Texts.PartialOutlineQualityName), Description = nameof(Texts.PartialOutlineQualityDesc), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 3d, 64d)]
        public Animation Quality { get; } = new Animation(64, 3, 256);

        [Display(GroupName = nameof(Texts.PartialOutlineOutlineGroupName), Name = nameof(Texts.PartialOutlineSmoothnessName), Description = nameof(Texts.PartialOutlineSmoothnessDesc), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Smoothness { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.PartialOutlineOutlineGroupName), Name = nameof(Texts.PartialOutlineIsAngularName), Description = nameof(Texts.PartialOutlineIsAngularDesc), Order = 3, ResourceType = typeof(Texts))]
        [ToggleSlider]
        [YMM4Only]
        public bool IsAngular
        {
            get => _isAngular;
            set => Set(ref _isAngular, value);
        }
        private bool _isAngular = false;

        [Display(GroupName = nameof(Texts.PartialOutlinePartialGroupName), Name = nameof(Texts.PartialOutlineAngleName), Description = nameof(Texts.PartialOutlineAngleDesc), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0d, 360d)]
        public Animation Angle { get; } = new Animation(90, 0, 360);

        [Display(GroupName = nameof(Texts.PartialOutlinePartialGroupName), Name = nameof(Texts.PartialOutlinePositionName), Description = nameof(Texts.PartialOutlinePositionDesc), Order = 11, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100d, 100d)]
        public Animation Position { get; } = new Animation(0, -200, 200);

        [Display(GroupName = nameof(Texts.PartialOutlinePartialGroupName), Name = nameof(Texts.PartialOutlineWidthName), Description = nameof(Texts.PartialOutlineWidthDesc), Order = 12, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Width { get; } = new Animation(50, 0, 200);

        [Display(GroupName = nameof(Texts.PartialOutlinePartialGroupName), Name = nameof(Texts.PartialOutlineSoftnessName), Description = nameof(Texts.PartialOutlineSoftnessDesc), Order = 13, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Softness { get; } = new Animation(30, 0, 100);

        [Display(GroupName = nameof(Texts.PartialOutlineAppearanceGroupName), Name = nameof(Texts.PartialOutlineOpacityName), Description = nameof(Texts.PartialOutlineOpacityDesc), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.PartialOutlineAppearanceGroupName), Name = nameof(Texts.PartialOutlineBlurName), Description = nameof(Texts.PartialOutlineBlurDesc), Order = 21, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 5d)]
        public Animation Blur { get; } = new Animation(0, 0, 1000);

        [Display(GroupName = nameof(Texts.PartialOutlineAppearanceGroupName), Name = nameof(Texts.PartialOutlineBlendName), Description = nameof(Texts.PartialOutlineBlendDesc), Order = 22, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Blend Blend
        {
            get => _blend;
            set => Set(ref _blend, value);
        }
        private Blend _blend = Blend.Normal;

        [Display(GroupName = nameof(Texts.PartialOutlineAppearanceGroupName), Name = nameof(Texts.PartialOutlineIsOutlineOnlyName), Description = nameof(Texts.PartialOutlineIsOutlineOnlyDesc), Order = 23, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsOutlineOnly
        {
            get => _isOutlineOnly;
            set => Set(ref _isOutlineOnly, value);
        }
        private bool _isOutlineOnly = false;

        [Display(GroupName = nameof(Texts.PartialOutlineBrushGroupName), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public Plugin.Brush.Brush Brush { get; } = new Plugin.Brush.Brush();

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new PartialOutlineEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [Thickness, Quality, Smoothness, Angle, Position, Width, Softness, Opacity, Blur, Brush];

        public override IEnumerable<string> GetFiles()
        {
            foreach (var file in base.GetFiles())
                yield return file;
            foreach (var file in Brush.GetFiles())
                yield return file;
        }

        public override void ReplaceFile(string from, string to)
        {
            base.ReplaceFile(from, to);
            Brush.ReplaceFile(from, to);
        }

        public override IEnumerable<TimelineResource> GetResources()
        {
            foreach (var resource in base.GetResources())
                yield return resource;
            foreach (var resource in Brush.GetResources())
                yield return resource;
        }
    }
}
