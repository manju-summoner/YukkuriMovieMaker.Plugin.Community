using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Transition.SpreadPageTurn;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.SpreadPageTurn
{
    [VideoEffect(nameof(Texts.SpreadPageTurnEffectName), [VideoEffectCategories.Drawing], ["見開きめくり", "本めくり", "ページめくり", "折りたたみ", "book", "spread", "page", "turn", "curl", "fold"], ResourceType = typeof(Texts))]
    internal class SpreadPageTurnEffect : VideoEffectBase
    {
        public override string Label => Texts.SpreadPageTurnEffectName;

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnEffectProgressName), Description = nameof(Texts.SpreadPageTurnEffectProgressDesc), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Progress { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnPageName), Description = nameof(Texts.SpreadPageTurnPageDesc), Order = 10, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpreadPageTurnPage Page { get => page; set => Set(ref page, value); }
        SpreadPageTurnPage page = SpreadPageTurnPage.Right;

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnStyleName), Description = nameof(Texts.SpreadPageTurnStyleDesc), Order = 15, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public SpreadPageTurnStyle Style { get => style; set => Set(ref style, value); }
        SpreadPageTurnStyle style = SpreadPageTurnStyle.Curl;

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnRadiusName), Description = nameof(Texts.SpreadPageTurnRadiusDesc), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 10, 500)]
        [ShowPropertyEditorWhen(nameof(Style), SpreadPageTurnStyle.Curl)]
        public Animation Radius { get; } = new Animation(200, 1, 4000);

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnFovName), Description = nameof(Texts.SpreadPageTurnFovDesc), Order = 25, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 179)]
        [ShowPropertyEditorWhen(nameof(Style), SpreadPageTurnStyle.Fold)]
        public Animation Fov { get; } = new Animation(SpreadPageTurnCustomEffect.DefaultFovDegrees, 0, 179.9);

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnShadowName), Description = nameof(Texts.SpreadPageTurnShadowDesc), Order = 30, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Shadow { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.SpreadPageTurnEffectName), Name = nameof(Texts.SpreadPageTurnBackLightnessName), Description = nameof(Texts.SpreadPageTurnBackLightnessDesc), Order = 40, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation BackLightness { get; } = new Animation(60, 0, 100);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new SpreadPageTurnEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Progress, Radius, Fov, Shadow, BackLightness];
    }
}
