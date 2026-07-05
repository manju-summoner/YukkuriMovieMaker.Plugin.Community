using System.ComponentModel.DataAnnotations;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;

namespace YukkuriMovieMaker.Plugin.Community.Transition.PageTurn
{
    internal sealed class PageTurnTransitionParameter : TransitionParameterBase
    {
        [Display(Name = nameof(Texts.PageTurnTransitionParameterOriginName), Description = nameof(Texts.PageTurnTransitionParameterOriginDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public PageTurnOrigin Origin { get => origin; set => Set(ref origin, value); }
        PageTurnOrigin origin = PageTurnOrigin.BottomRight;

        [Display(Name = nameof(Texts.PageTurnTransitionParameterRadiusName), Description = nameof(Texts.PageTurnTransitionParameterRadiusDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 10, 500)]
        public Animation Radius { get; } = new Animation(200, 1, 4000);

        [Display(Name = nameof(Texts.PageTurnTransitionParameterShadowName), Description = nameof(Texts.PageTurnTransitionParameterShadowDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Shadow { get; } = new Animation(50, 0, 100);

        [Display(Name = nameof(Texts.PageTurnTransitionParameterBackLightnessName), Description = nameof(Texts.PageTurnTransitionParameterBackLightnessDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation BackLightness { get; } = new Animation(60, 0, 100);

        [Display(Name = nameof(Texts.EasingTypeName), Description = nameof(Texts.EasingTypeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Sine;

        [Display(Name = nameof(Texts.EasingModeName), Description = nameof(Texts.EasingModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.InOut;

        public override ITransitionSource CreateTransition(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after)
            => new PageTurnTransitionSource(devices, before, after, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Radius, Shadow, BackLightness];
    }
}
