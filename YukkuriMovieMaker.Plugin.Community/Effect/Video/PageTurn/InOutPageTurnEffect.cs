using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Transition.PageTurn;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PageTurn
{
    [VideoEffect(nameof(Texts.InOutPageTurnEffectName), [VideoEffectCategories.Transition], ["ページめくり", "page", "turn", "curl"], ResourceType = typeof(Texts))]
    internal class InOutPageTurnEffect : InOutEffectBase
    {
        public override string Label => CreateLabelText(Texts.InPageTurnEffectLabel, Texts.OutPageTurnEffectLabel);

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


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new InOutPageTurnEffectProcessor(devices, this);
    }
}
