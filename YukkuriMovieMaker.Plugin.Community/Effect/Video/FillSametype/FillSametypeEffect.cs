using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSametype
{
    [VideoEffect(nameof(Texts.FillSametypeEffectName), [VideoEffectCategories.Decoration], ["fill", "同型", "塗りつぶし", "形状"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class FillSametypeEffect : VideoEffectBase
    {
        public override string Label => Texts.FillSametypeEffectName;

        [Display(GroupName = nameof(Texts.FillSametypeEffectName), Name = nameof(Texts.FillSametypeOpacityName), ResourceType = typeof(Texts), Order = 0)]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.FillSametypeEffectName), Name = nameof(Texts.FillSametypeBlurName), ResourceType = typeof(Texts), Order = 10)]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(0, 0, 100000);

        [Display(GroupName = nameof(Texts.FillSametypeEffectName), Name = nameof(Texts.FillSametypeBlendModeName), ResourceType = typeof(Texts), Order = 20)]
        [EnumComboBox]
        public Blend BlendMode
        {
            get => blendMode;
            set => Set(ref blendMode, value);
        }
        Blend blendMode = Blend.Normal;

        [Display(GroupName = nameof(Texts.FillSametypeEffectName), Name = nameof(Texts.FillSametypeIsInvertedName), ResourceType = typeof(Texts), Order = 30)]
        [ToggleSlider]
        public bool IsInverted
        {
            get => isInverted;
            set => Set(ref isInverted, value);
        }
        bool isInverted;

        [Display(GroupName = nameof(Texts.FillSametypeEffectName), Name = nameof(Texts.FillSametypeIsBrushOnlyName), ResourceType = typeof(Texts), Order = 40)]
        [ToggleSlider]
        public bool IsBrushOnly
        {
            get => isBrushOnly;
            set => Set(ref isBrushOnly, value);
        }
        bool isBrushOnly;

        [Display(GroupName = nameof(Texts.FillSametypeEffectName), Name = nameof(Texts.FillSametypePreserveLuminanceName), ResourceType = typeof(Texts), Order = 45)]
        [ToggleSlider]
        public bool PreserveLuminance
        {
            get => preserveLuminance;
            set => Set(ref preserveLuminance, value);
        }
        bool preserveLuminance;

        [Display(GroupName = nameof(Texts.FillSametypeTargetGroupName), Name = nameof(Texts.FillSametypeXName), ResourceType = typeof(Texts), Order = 110)]
        [AnimationSlider("F1", "px", -2000, 2000)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.FillSametypeTargetGroupName), Name = nameof(Texts.FillSametypeYName), ResourceType = typeof(Texts), Order = 111)]
        [AnimationSlider("F1", "px", -2000, 2000)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.FillSametypeTargetGroupName), Name = nameof(Texts.FillSametypeToleranceName), ResourceType = typeof(Texts), Order = 130)]
        [AnimationSlider("F0", "", 0, 255)]
        public Animation Tolerance { get; } = new Animation(15, 0, 255);

        [Display(GroupName = nameof(Texts.FillSametypeTargetGroupName), Name = nameof(Texts.FillSametypeShapeThresholdName), ResourceType = typeof(Texts), Order = 140)]
        [AnimationSlider("F2", "", 0, 100)]
        public Animation ShapeThreshold { get; } = new Animation(20, 0, 100);

        [Display(GroupName = nameof(Texts.FillSametypeBrushGroupName), Order = 200, AutoGenerateField = true, ResourceType = typeof(Texts))]
        public YukkuriMovieMaker.Plugin.Brush.Brush Brush { get; } = new YukkuriMovieMaker.Plugin.Brush.Brush();

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new FillSametypeProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, Blur, X, Y, Tolerance, ShapeThreshold, Brush];
    }
}
