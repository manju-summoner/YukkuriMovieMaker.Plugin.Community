using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FillSameground
{
    [VideoEffect(nameof(Texts.FillSamegroundEffectName), [VideoEffectCategories.Decoration], ["fill", "同色", "塗りつぶし"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class FillSamegroundEffect : VideoEffectBase
    {
        public override string Label => Texts.FillSamegroundEffectName;

        [Display(GroupName = nameof(Texts.FillSamegroundEffectName), Name = nameof(Texts.FillSamegroundOpacityName), ResourceType = typeof(Texts), Order = 0)]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.FillSamegroundEffectName), Name = nameof(Texts.FillSamegroundBlurName), ResourceType = typeof(Texts), Order = 10)]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(0, 0, 100000);

        [Display(GroupName = nameof(Texts.FillSamegroundEffectName), Name = nameof(Texts.FillSamegroundBlendModeName), ResourceType = typeof(Texts), Order = 20)]
        [EnumComboBox]
        public Blend BlendMode
        {
            get => blendMode;
            set => Set(ref blendMode, value);
        }
        Blend blendMode = Blend.Normal;

        [Display(GroupName = nameof(Texts.FillSamegroundEffectName), Name = nameof(Texts.FillSamegroundIsInvertedName), ResourceType = typeof(Texts), Order = 30)]
        [ToggleSlider]
        public bool IsInverted
        {
            get => isInverted;
            set => Set(ref isInverted, value);
        }
        bool isInverted;

        [Display(GroupName = nameof(Texts.FillSamegroundEffectName), Name = nameof(Texts.FillSamegroundIsBrushOnlyName), ResourceType = typeof(Texts), Order = 40)]
        [ToggleSlider]
        public bool IsBrushOnly
        {
            get => isBrushOnly;
            set => Set(ref isBrushOnly, value);
        }
        bool isBrushOnly;

        [Display(GroupName = nameof(Texts.FillSamegroundEffectName), Name = nameof(Texts.FillSamegroundPreserveLuminanceName), ResourceType = typeof(Texts), Order = 45)]
        [ToggleSlider]
        public bool PreserveLuminance
        {
            get => preserveLuminance;
            set => Set(ref preserveLuminance, value);
        }
        bool preserveLuminance;

        [Display(GroupName = nameof(Texts.FillSamegroundTargetGroupName), Name = nameof(Texts.FillSamegroundModeName), ResourceType = typeof(Texts), Order = 100)]
        [EnumComboBox]
        public FillSamegroundMode Mode
        {
            get => mode;
            set => Set(ref mode, value);
        }
        FillSamegroundMode mode = FillSamegroundMode.Position;

        [Display(GroupName = nameof(Texts.FillSamegroundTargetGroupName), Name = nameof(Texts.FillSamegroundXName), ResourceType = typeof(Texts), Order = 110)]
        [AnimationSlider("F1", "px", -2000, 2000)]
        [ShowPropertyEditorWhen(nameof(Mode), FillSamegroundMode.Position | FillSamegroundMode.PositionColor)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.FillSamegroundTargetGroupName), Name = nameof(Texts.FillSamegroundYName), ResourceType = typeof(Texts), Order = 111)]
        [AnimationSlider("F1", "px", -2000, 2000)]
        [ShowPropertyEditorWhen(nameof(Mode), FillSamegroundMode.Position | FillSamegroundMode.PositionColor)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.FillSamegroundTargetGroupName), Name = nameof(Texts.FillSamegroundTargetColorName), ResourceType = typeof(Texts), Order = 120)]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(Mode), FillSamegroundMode.Color)]
        public Color TargetColor
        {
            get => targetColor;
            set => Set(ref targetColor, value);
        }
        Color targetColor = Color.FromRgb(255, 0, 0);

        [Display(GroupName = nameof(Texts.FillSamegroundTargetGroupName), Name = nameof(Texts.FillSamegroundToleranceName), ResourceType = typeof(Texts), Order = 130)]
        [AnimationSlider("F0", "", 0, 255)]
        public Animation Tolerance { get; } = new Animation(15, 0, 255);

        [Display(GroupName = nameof(Texts.FillSamegroundBrushGroupName), Order = 200, AutoGenerateField = true, ResourceType = typeof(Texts))]
        public YukkuriMovieMaker.Plugin.Brush.Brush Brush { get; } = new YukkuriMovieMaker.Plugin.Brush.Brush();

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new FillSamegroundProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, Blur, X, Y, Tolerance, Brush];
    }
}
