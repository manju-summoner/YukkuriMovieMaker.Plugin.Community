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
    [VideoEffect("同色塗りつぶし", ["装飾"], ["fill"], IsAviUtlSupported = false)]
    public class FillSamegroundEffect : VideoEffectBase
    {
        public override string Label => "同色塗りつぶし";

        [Display(GroupName = "同色塗りつぶし", Name = "不透明度", Order = 0)]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = "同色塗りつぶし", Name = "境界ぼかし", Order = 10)]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Blur { get; } = new Animation(0, 0, 100000);

        [Display(GroupName = "同色塗りつぶし", Name = "合成モード", Order = 20)]
        [EnumComboBox]
        public Blend BlendMode
        {
            get => blendMode;
            set => Set(ref blendMode, value);
        }
        Blend blendMode = Blend.Normal;

        [Display(GroupName = "同色塗りつぶし", Name = "領域反転", Order = 30)]
        [ToggleSlider]
        public bool IsInverted
        {
            get => isInverted;
            set => Set(ref isInverted, value);
        }
        bool isInverted;

        [Display(GroupName = "同色塗りつぶし", Name = "模様のみ", Order = 40)]
        [ToggleSlider]
        public bool IsBrushOnly
        {
            get => isBrushOnly;
            set => Set(ref isBrushOnly, value);
        }
        bool isBrushOnly;

        [Display(GroupName = "同色塗りつぶし", Name = "輝度保持", Order = 45)]
        [ToggleSlider]
        public bool PreserveLuminance
        {
            get => preserveLuminance;
            set => Set(ref preserveLuminance, value);
        }
        bool preserveLuminance;

        [Display(GroupName = "対象", Name = "モード", Order = 100)]
        [EnumComboBox]
        public FillSamegroundMode Mode
        {
            get => mode;
            set => Set(ref mode, value);
        }
        FillSamegroundMode mode = FillSamegroundMode.Position;

        [Display(GroupName = "対象", Name = "X", Order = 110)]
        [AnimationSlider("F1", "px", -2000, 2000)]
        [ShowPropertyEditorWhen(nameof(Mode), FillSamegroundMode.Position | FillSamegroundMode.PositionColor)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "対象", Name = "Y", Order = 111)]
        [AnimationSlider("F1", "px", -2000, 2000)]
        [ShowPropertyEditorWhen(nameof(Mode), FillSamegroundMode.Position | FillSamegroundMode.PositionColor)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = "対象", Name = "対象色", Order = 120)]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(Mode), FillSamegroundMode.Color)]
        public Color TargetColor
        {
            get => targetColor;
            set => Set(ref targetColor, value);
        }
        Color targetColor = Color.FromRgb(255, 0, 0);

        [Display(GroupName = "対象", Name = "許容範囲", Order = 130)]
        [AnimationSlider("F0", "", 0, 128)]
        public Animation Tolerance { get; } = new Animation(15, 0, 255);

        [Display(GroupName = "模様", Order = 200, AutoGenerateField = true)]
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
