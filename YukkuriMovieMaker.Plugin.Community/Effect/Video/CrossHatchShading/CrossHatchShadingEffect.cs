using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CrossHatchShading
{
    [VideoEffect(nameof(Texts.CrossHatchShadingEffectName), [VideoEffectCategories.Filtering], [nameof(Texts.TagCrossHatch), nameof(Texts.TagHatching), nameof(Texts.TagPen), nameof(Texts.TagEngraving), nameof(Texts.TagBlueprint), nameof(Texts.TagManga)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public sealed class CrossHatchShadingEffect : VideoEffectBase
    {
        public override string Label => Texts.CrossHatchShadingEffectName;

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingStyleName), Description = nameof(Texts.CrossHatchShadingStyleDesc), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public CrossHatchStyle Style { get => style; set => Set(ref style, value); }
        CrossHatchStyle style = CrossHatchStyle.Pen;

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingAmountName), Description = nameof(Texts.CrossHatchShadingAmountDesc), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Amount { get; } = new(100, 0, 100);

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingDensityName), Description = nameof(Texts.CrossHatchShadingDensityDesc), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 3d, 80d)]
        public Animation Density { get; } = new(12, 3, 320);

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingThicknessName), Description = nameof(Texts.CrossHatchShadingThicknessDesc), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0.2d, 12d)]
        public Animation Thickness { get; } = new(1.2, 0.2, 48);

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingShadowDepthName), Description = nameof(Texts.CrossHatchShadingShadowDepthDesc), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 200d)]
        public Animation ShadowDepth { get; } = new(100, 0, 200);

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingFlowName), Description = nameof(Texts.CrossHatchShadingFlowDesc), Order = 50, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Flow { get; } = new(40, 0, 100);

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingWobbleName), Description = nameof(Texts.CrossHatchShadingWobbleDesc), Order = 55, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Wobble { get; } = new(25, 0, 100);

        [Display(GroupName = nameof(Texts.CrossHatchShadingBasicGroup), Name = nameof(Texts.CrossHatchShadingOutlineName), Description = nameof(Texts.CrossHatchShadingOutlineDesc), Order = 60, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Outline { get; } = new(30, 0, 100);

        [Display(GroupName = nameof(Texts.CrossHatchShadingColorGroup), Name = nameof(Texts.CrossHatchShadingInkColorName), Description = nameof(Texts.CrossHatchShadingInkColorDesc), Order = 100, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color InkColor { get => inkColor; set => Set(ref inkColor, value); }
        Color inkColor = Color.FromArgb(255, 17, 17, 17);

        [Display(GroupName = nameof(Texts.CrossHatchShadingColorGroup), Name = nameof(Texts.CrossHatchShadingPaperColorName), Description = nameof(Texts.CrossHatchShadingPaperColorDesc), Order = 110, ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color PaperColor { get => paperColor; set => Set(ref paperColor, value); }
        Color paperColor = Color.FromArgb(255, 246, 241, 231);

        [Display(GroupName = nameof(Texts.CrossHatchShadingColorGroup), Name = nameof(Texts.CrossHatchShadingColorModeName), Description = nameof(Texts.CrossHatchShadingColorModeDesc), Order = 120, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public CrossHatchColorMode ColorMode { get => colorMode; set => Set(ref colorMode, value); }
        CrossHatchColorMode colorMode = CrossHatchColorMode.InkAndPaper;

        [Display(GroupName = nameof(Texts.CrossHatchShadingDetailGroup), Name = nameof(Texts.CrossHatchShadingLineLayersName), Description = nameof(Texts.CrossHatchShadingLineLayersDesc), Order = 200, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public CrossHatchLineLayers LineLayers { get => lineLayers; set => Set(ref lineLayers, value); }
        CrossHatchLineLayers lineLayers = CrossHatchLineLayers.Auto;

        [Display(GroupName = nameof(Texts.CrossHatchShadingDetailGroup), Name = nameof(Texts.CrossHatchShadingToneSoftnessName), Description = nameof(Texts.CrossHatchShadingToneSoftnessDesc), Order = 210, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 50d)]
        public Animation ToneSoftness { get; } = new(12, 0, 50);

        [Display(GroupName = nameof(Texts.CrossHatchShadingDetailGroup), Name = nameof(Texts.CrossHatchShadingGrainName), Description = nameof(Texts.CrossHatchShadingGrainDesc), Order = 220, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 100d)]
        public Animation Grain { get; } = new(12, 0, 100);

        [Display(GroupName = nameof(Texts.CrossHatchShadingDetailGroup), Name = nameof(Texts.CrossHatchShadingSeedName), Description = nameof(Texts.CrossHatchShadingSeedDesc), Order = 230, ResourceType = typeof(Texts))]
        [Range(0, 9999)]
        [DefaultValue(0)]
        [TextBoxSlider("F0", "", 0, 9999)]
        public int Seed { get => seed; set => Set(ref seed, Math.Clamp(value, 0, 9999)); }
        int seed;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new CrossHatchShadingEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Amount, Density, Thickness, ShadowDepth, Flow, Wobble, Outline, ToneSoftness, Grain];
    }
}
