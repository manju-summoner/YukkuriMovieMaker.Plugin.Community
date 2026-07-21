using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LineHighlight
{
    [VideoEffect(nameof(Texts.LineHighlight), [VideoEffectCategories.Animation], ["line highlight"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class LineHighlightEffect : VideoEffectBase
    {
        public override string Label => Texts.LineHighlight;

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.Color), Description = nameof(Texts.ColorDesc), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.Strength), Description = nameof(Texts.Strength), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.Fade), Description = nameof(Texts.FadeDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Fade { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.Size), Description = nameof(Texts.SizeDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Size { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.Blur), Description = nameof(Texts.BlurDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation Blur { get; } = new Animation(50, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.Angle), Description = nameof(Texts.AngleDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(-45, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.EasingType), Description = nameof(Texts.EasingTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Expo;

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.EasingMode), Description = nameof(Texts.EasingModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.Out;

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.EffectDuration), Description = nameof(Texts.EffectDurationDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), 0, 1, ResourceType = typeof(Texts))]
        public Animation EffectDuration { get; } = new Animation(1, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.IsLoop), Description = nameof(Texts.IsLoopDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsLoop { get => isLoop; set => Set(ref isLoop, value); }
        bool isLoop = false;

        [Display(GroupName = nameof(Texts.LineHighlight), Name = nameof(Texts.LoopInterval), Description = nameof(Texts.LoopIntervalDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), 0, 1, ResourceType = typeof(Texts))]
        [ShowPropertyEditorWhen(nameof(IsLoop), true)]
        public Animation LoopInterval { get; } = new Animation(1, 0, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new LineHighlightEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength, Fade, Size, Blur, Angle, EffectDuration, LoopInterval];
    }
}
