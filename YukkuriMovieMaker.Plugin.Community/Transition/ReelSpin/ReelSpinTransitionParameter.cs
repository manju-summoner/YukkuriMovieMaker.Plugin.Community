using System.ComponentModel.DataAnnotations;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.ReelSpin
{
    internal sealed class ReelSpinTransitionParameter : TransitionParameterBase
    {
        [Display(Name = nameof(Texts.ReelSpinTransitionPattern), Description = nameof(Texts.ReelSpinTransitionPattern), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ReelSpinTransitionPattern Pattern { get => pattern; set => Set(ref pattern, value); }
        ReelSpinTransitionPattern pattern = ReelSpinTransitionPattern.Alternate;

        [Display(Name = nameof(Texts.ReelSpinTransitionLaps), Description = nameof(Texts.ReelSpinTransitionLaps), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", nameof(Texts.ReelSpinTransitionLapsUnit), 1, 5, ResourceType = typeof(Texts))]
        public Animation Laps { get; } = new Animation(1, 1, 100);

        [Display(Name = nameof(Texts.ReelSpinTransitionDirection), Description = nameof(Texts.ReelSpinTransitionDirection), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "°", 0, 360)]
        public Animation Direction { get; } = new Animation(90, 0, 360);

        [Display(Name = nameof(Texts.ReelSpinTransitionBlur), Description = nameof(Texts.ReelSpinTransitionBlur), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Blur { get; } = new Animation(50, 0, 100);

        //OFF: リール方向に整列するレンガ積み配置（斜めでも隙間なし）
        //ON: XY固定格子の敷き詰め
        [Display(Name = nameof(Texts.ReelSpinTransitionTile), Description = nameof(Texts.ReelSpinTransitionTile), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Tile { get => tile; set => Set(ref tile, value); }
        bool tile = false;

        [Display(Name = nameof(Texts.EasingTypeName), Description = nameof(Texts.EasingTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Sine;

        [Display(Name = nameof(Texts.EasingModeName), Description = nameof(Texts.EasingModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.InOut;

        public override ITransitionSource CreateTransition(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after)
            => new ReelSpinTransitionSource(devices, before, after, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Laps, Direction, Blur];
    }
}
