using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Transition.PageTurn;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PageTurn
{
    [VideoEffect(nameof(Texts.InOutPageTurnEffectName), [VideoEffectCategories.Transition], ["ページめくり", "page", "turn", "curl"], ResourceType = typeof(Texts))]
    internal class InOutPageTurnEffect : VideoEffectBase
    {
        public override string Label => IsInEffect ? Texts.InPageTurnEffectLabel : Texts.OutPageTurnEffectLabel;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.PageTurnTransitionParameterOriginName), Description = nameof(Texts.PageTurnTransitionParameterOriginDesc), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public PageTurnOrigin Origin { get => origin; set => Set(ref origin, value); }
        PageTurnOrigin origin = PageTurnOrigin.BottomRight;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.PageTurnTransitionParameterRadiusName), Description = nameof(Texts.PageTurnTransitionParameterRadiusDesc), Order = 10, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "px", 10, 500)]
        [Range(1d, 4000d)]
        [DefaultValue(200d)]
        public double Radius { get => radius; set => Set(ref radius, value); }
        double radius = 200;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.PageTurnTransitionParameterShadowName), Description = nameof(Texts.PageTurnTransitionParameterShadowDesc), Order = 20, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(50d)]
        public double Shadow { get => shadow; set => Set(ref shadow, value); }
        double shadow = 50;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.PageTurnTransitionParameterBackLightnessName), Description = nameof(Texts.PageTurnTransitionParameterBackLightnessDesc), Order = 30, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(60d)]
        public double BackLightness { get => backLightness; set => Set(ref backLightness, value); }
        double backLightness = 60;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.InOutPageTurnEffectIsInEffectName), Description = nameof(Texts.InOutPageTurnEffectIsInEffectDesc), Order = 100, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInEffect { get => isInEffect; set => Set(ref isInEffect, value); }
        bool isInEffect = true;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.InOutPageTurnEffectIsOutEffectName), Description = nameof(Texts.InOutPageTurnEffectIsOutEffectDesc), Order = 200, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsOutEffect { get => isOutEffect; set => Set(ref isOutEffect, value); }
        bool isOutEffect = true;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.InOutPageTurnEffectEffectTimeSecondsName), Description = nameof(Texts.InOutPageTurnEffectEffectTimeSecondsDesc), Order = 300, ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.InOutPageTurnEffectSecUnit), 0, 0.5, ResourceType = typeof(Texts))]
        [Range(0d, YMM4Constants.VeryLargeValue)]
        [DefaultValue(0.3d)]
        public double EffectTimeSeconds { get => effectTimeSeconds; set => Set(ref effectTimeSeconds, value); }
        double effectTimeSeconds = 0.30;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.InOutPageTurnEffectEasingTypeName), Description = nameof(Texts.InOutPageTurnEffectEasingTypeDesc), Order = 400, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Expo;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.InOutPageTurnEffectEasingModeName), Description = nameof(Texts.InOutPageTurnEffectEasingModeDesc), Order = 500, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.Out;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new InOutPageTurnEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
