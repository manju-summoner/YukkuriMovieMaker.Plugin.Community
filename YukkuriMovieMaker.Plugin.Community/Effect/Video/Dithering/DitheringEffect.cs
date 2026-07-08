using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Dithering
{
    [VideoEffect(nameof(Texts.Dithering), [VideoEffectCategories.Filtering], [nameof(Texts.TagDithering), nameof(Texts.TagRetro), nameof(Texts.TagPixelArt)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class DitheringEffect : VideoEffectBase
    {
        public override string Label => Texts.Dithering;

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.Mode), Description = nameof(Texts.ModeDescription), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public DitheringMode Mode
        {
            get => _mode;
            set => Set(ref _mode, value);
        }
        private DitheringMode _mode = DitheringMode.Rgb;

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.Levels), Description = nameof(Texts.LevelsDescription), Order = 1, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 2, 16)]
        public Animation Levels { get; } = new Animation(4, 2, 256);

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.Scale), Description = nameof(Texts.ScaleDescription), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 32)]
        public Animation Scale { get; } = new Animation(1, 1, 512);

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.Strength), Description = nameof(Texts.StrengthDescription), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.DarkColor), Description = nameof(Texts.DarkColorDescription), Order = 4, ResourceType = typeof(Texts))]
        [ColorPicker]
        [DuotoneColorVisible]
        public Color DarkColor
        {
            get => _darkColor;
            set => Set(ref _darkColor, value);
        }
        private Color _darkColor = Color.FromArgb(255, 15, 56, 15);

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.LightColor), Description = nameof(Texts.LightColorDescription), Order = 5, ResourceType = typeof(Texts))]
        [ColorPicker]
        [DuotoneColorVisible]
        public Color LightColor
        {
            get => _lightColor;
            set => Set(ref _lightColor, value);
        }
        private Color _lightColor = Color.FromArgb(255, 155, 188, 15);

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.BlendMode), Description = nameof(Texts.BlendModeDescription), Order = 6, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public Blend BlendMode
        {
            get => _blendMode;
            set => Set(ref _blendMode, value);
        }
        private Blend _blendMode = Blend.Normal;

        [Display(GroupName = nameof(Texts.Dithering), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 7, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Amount { get; } = new Animation(100, 0, 100);

        private IAnimatable[]? _animatables;

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex,
            ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new DitheringEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => _animatables ??= [Levels, Scale, Strength, Amount];
    }
}
