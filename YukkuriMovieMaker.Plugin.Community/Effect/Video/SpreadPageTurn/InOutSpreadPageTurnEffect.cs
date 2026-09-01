using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.SpreadPageTurn
{
    [VideoEffect(nameof(Texts.InOutSpreadPageTurnEffectName), [VideoEffectCategories.Transition], ["見開きめくり", "本めくり", "ページめくり", "折りたたみ", "book", "spread", "page", "turn", "curl", "fold"], ResourceType = typeof(Texts))]
    internal class InOutSpreadPageTurnEffect : InOutEffectBase
    {
        public override string Label => CreateLabelText(Texts.InSpreadPageTurnEffectLabel, Texts.OutSpreadPageTurnEffectLabel);

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.SpreadPageTurnPageName), Description = nameof(Texts.SpreadPageTurnPageDesc), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpreadPageTurnPage Page { get => page; set => Set(ref page, value); }
        SpreadPageTurnPage page = SpreadPageTurnPage.Right;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.SpreadPageTurnStyleName), Description = nameof(Texts.SpreadPageTurnStyleDesc), Order = 5, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpreadPageTurnStyle Style { get => style; set => Set(ref style, value); }
        SpreadPageTurnStyle style = SpreadPageTurnStyle.Curl;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.SpreadPageTurnRadiusName), Description = nameof(Texts.SpreadPageTurnRadiusDesc), Order = 10, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "px", 10, 500)]
        [Range(1d, 4000d)]
        [DefaultValue(200d)]
        [ShowPropertyEditorWhen(nameof(Style), SpreadPageTurnStyle.Curl)]
        public double Radius { get => radius; set => Set(ref radius, value); }
        double radius = 200;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.SpreadPageTurnFovName), Description = nameof(Texts.SpreadPageTurnFovDesc), Order = 15, ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "°", 0, 179)]
        [Range(0d, 179.9d)]
        [DefaultValue(SpreadPageTurnCustomEffect.DefaultFovDegrees)]
        [ShowPropertyEditorWhen(nameof(Style), SpreadPageTurnStyle.Fold)]
        public double Fov { get => fov; set => Set(ref fov, value); }
        double fov = SpreadPageTurnCustomEffect.DefaultFovDegrees;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.SpreadPageTurnShadowName), Description = nameof(Texts.SpreadPageTurnShadowDesc), Order = 20, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(50d)]
        public double Shadow { get => shadow; set => Set(ref shadow, value); }
        double shadow = 50;

        [Display(GroupName = nameof(Texts.InOutGroupName), Name = nameof(Texts.SpreadPageTurnBackLightnessName), Description = nameof(Texts.SpreadPageTurnBackLightnessDesc), Order = 30, ResourceType = typeof(Texts))]
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
            => new InOutSpreadPageTurnEffectProcessor(devices, this);
    }
}
