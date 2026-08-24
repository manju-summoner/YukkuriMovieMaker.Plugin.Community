using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Transition.PageTurn;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PageTurn
{
    [VideoEffect(nameof(Texts.PageTurnEffectName), [VideoEffectCategories.Drawing], ["ページめくり", "page", "turn", "curl"], ResourceType = typeof(Texts))]
    internal class PageTurnEffect : VideoEffectBase
    {
        public override string Label => Texts.PageTurnEffectName;

        [Display(GroupName = nameof(Texts.PageTurnEffectName), Name = nameof(Texts.PageTurnEffectProgressName), Description = nameof(Texts.PageTurnEffectProgressDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Progress { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.PageTurnEffectName), Name = nameof(Texts.PageTurnTransitionParameterOriginName), Description = nameof(Texts.PageTurnTransitionParameterOriginDesc), Order = 10, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public PageTurnOrigin Origin { get => origin; set => Set(ref origin, value); }
        PageTurnOrigin origin = PageTurnOrigin.BottomRight;

        [Display(GroupName = nameof(Texts.PageTurnEffectName), Name = nameof(Texts.PageTurnTransitionParameterRadiusName), Description = nameof(Texts.PageTurnTransitionParameterRadiusDesc), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 10, 500)]
        public Animation Radius { get; } = new Animation(200, 1, 4000);

        [Display(GroupName = nameof(Texts.PageTurnEffectName), Name = nameof(Texts.PageTurnTransitionParameterShadowName), Description = nameof(Texts.PageTurnTransitionParameterShadowDesc), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Shadow { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.PageTurnEffectName), Name = nameof(Texts.PageTurnTransitionParameterBackLightnessName), Description = nameof(Texts.PageTurnTransitionParameterBackLightnessDesc), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation BackLightness { get; } = new Animation(60, 0, 100);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new PageTurnEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Progress, Radius, Shadow, BackLightness];
    }
}
