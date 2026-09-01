using System.ComponentModel.DataAnnotations;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn
{
    internal sealed class SpreadPageTurnTransitionParameter : TransitionParameterBase
    {
        [Display(Name = nameof(Texts.SpreadPageTurnPageName), Description = nameof(Texts.SpreadPageTurnPageDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpreadPageTurnPage Page { get => page; set => Set(ref page, value); }
        SpreadPageTurnPage page = SpreadPageTurnPage.Right;

        [Display(Name = nameof(Texts.SpreadPageTurnStyleName), Description = nameof(Texts.SpreadPageTurnStyleDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpreadPageTurnStyle Style { get => style; set => Set(ref style, value); }
        SpreadPageTurnStyle style = SpreadPageTurnStyle.Curl;

        [Display(Name = nameof(Texts.SpreadPageTurnRadiusName), Description = nameof(Texts.SpreadPageTurnRadiusDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 10, 500)]
        [ShowPropertyEditorWhen(nameof(Style), SpreadPageTurnStyle.Curl)]
        public Animation Radius { get; } = new Animation(200, 1, 4000);

        [Display(Name = nameof(Texts.SpreadPageTurnFovName), Description = nameof(Texts.SpreadPageTurnFovDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 179)]
        [ShowPropertyEditorWhen(nameof(Style), SpreadPageTurnStyle.Fold)]
        public Animation Fov { get; } = new Animation(SpreadPageTurnCustomEffect.DefaultFovDegrees, 0, 179.9);

        [Display(Name = nameof(Texts.SpreadPageTurnShadowName), Description = nameof(Texts.SpreadPageTurnShadowDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Shadow { get; } = new Animation(50, 0, 100);

        [Display(Name = nameof(Texts.EasingTypeName), Description = nameof(Texts.EasingTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Sine;

        [Display(Name = nameof(Texts.EasingModeName), Description = nameof(Texts.EasingModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.InOut;

        public override ITransitionSource CreateTransition(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after)
            => new SpreadPageTurnTransitionSource(devices, before, after, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Radius, Fov, Shadow];
    }
}
