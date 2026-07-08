using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Kaleidoscope
{
    [VideoEffect(nameof(Texts.Kaleidoscope), [VideoEffectCategories.Filtering], [nameof(Texts.TagKaleidoscope), nameof(Texts.TagMirror), nameof(Texts.TagSymmetry)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class KaleidoscopeEffect : VideoEffectBase
    {
        public override string Label => Texts.Kaleidoscope;

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.Segments), Description = nameof(Texts.SegmentsDescription), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 2, 24)]
        public Animation Segments { get; } = new Animation(6, 1, 256);

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.Rotation), Description = nameof(Texts.RotationDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation Rotation { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.Zoom), Description = nameof(Texts.ZoomDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 10, 400)]
        public Animation Zoom { get; } = new Animation(100, 1, 2000);

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.CenterX), Description = nameof(Texts.CenterXDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation CenterX { get; } = new Animation(0, -500, 500);

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.CenterY), Description = nameof(Texts.CenterYDescription), Order = 4, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation CenterY { get; } = new Animation(0, -500, 500);

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Amount { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.Kaleidoscope), Name = nameof(Texts.Mirror), Description = nameof(Texts.MirrorDescription), Order = 6, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Mirror
        {
            get => _mirror;
            set => Set(ref _mirror, value);
        }
        private bool _mirror = true;

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex,
            ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new KaleidoscopeEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Segments, Rotation, Zoom, CenterX, CenterY, Amount];
    }
}
