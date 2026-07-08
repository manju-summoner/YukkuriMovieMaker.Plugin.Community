using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReelSpin
{
    [VideoEffect(nameof(Texts.InOutReelSpinEffectName), [VideoEffectCategories.Transition], ["reel", "slot", "リール回転", "スロット"], ResourceType = typeof(Texts))]
    internal class InOutReelSpinEffect : InOutEffectBase
    {
        public override string Label => CreateLabelText(Texts.InReelSpinEffectLabel, Texts.OutReelSpinEffectLabel);

        [Display(GroupName = nameof(Texts.ReelSpinInOutGroupName), Name = nameof(Texts.ReelSpinLaps), Description = nameof(Texts.ReelSpinLaps), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", nameof(Texts.ReelSpinLapsUnit), 0, 5, ResourceType = typeof(Texts))]
        [Range(0d, 100d)]
        [DefaultValue(1d)]
        public double Laps { get => laps; set => Set(ref laps, value); }
        double laps = 1;

        [Display(GroupName = nameof(Texts.ReelSpinInOutGroupName), Name = nameof(Texts.ReelSpinDirection), Description = nameof(Texts.ReelSpinDirection), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "°", 0, 360)]
        [Range(0d, 360d)]
        [DefaultValue(90d)]
        public double Direction { get => direction; set => Set(ref direction, value); }
        double direction = 90;

        [Display(GroupName = nameof(Texts.ReelSpinInOutGroupName), Name = nameof(Texts.ReelSpinBlur), Description = nameof(Texts.ReelSpinBlur), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "%", 0, 100)]
        [Range(0d, 100d)]
        [DefaultValue(50d)]
        public double Blur { get => blur; set => Set(ref blur, value); }
        double blur = 50;

        //OFF: リール方向に整列するレンガ積み配置（斜めでも隙間なし）
        //ON: XY固定格子の敷き詰め
        [Display(GroupName = nameof(Texts.ReelSpinInOutGroupName), Name = nameof(Texts.ReelSpinTile), Description = nameof(Texts.ReelSpinTile), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Tile { get => tile; set => Set(ref tile, value); }
        bool tile = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new InOutReelSpinEffectProcessor(devices, this);
    }
}
