using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReelSpin
{
    [VideoEffect(nameof(Texts.ReelSpinEffectName), [VideoEffectCategories.Animation], ["reel", "slot", "リール回転", "スロット", "周回", "loop scroll"], ResourceType = typeof(Texts))]
    internal class ReelSpinEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.ReelSpinEffectName} {Rotation.GetValue(0, 1, 30):F0}% {Direction.GetValue(0, 1, 30):F0}°";

        //回転位置。100%で1周して元に戻る
        [Display(GroupName = nameof(Texts.ReelSpinEffectName), Name = nameof(Texts.ReelSpinRotation), Description = nameof(Texts.ReelSpinRotation), Order = 0, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation Rotation { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        //コンテンツの移動方向（0°で右、90°で下）
        [Display(GroupName = nameof(Texts.ReelSpinEffectName), Name = nameof(Texts.ReelSpinDirection), Description = nameof(Texts.ReelSpinDirection), Order = 10, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Direction { get; } = new Animation(90, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ReelSpinEffectName), Name = nameof(Texts.ReelSpinBlur), Description = nameof(Texts.ReelSpinBlur), Order = 20, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Blur { get; } = new Animation(50, 0, 100);

        //OFF: リール方向に整列するレンガ積み配置（斜めでも隙間なし・1周で必ず元に戻る）
        //ON: XY固定格子の敷き詰め（斜めでは1周しても元に戻らない）
        [Display(GroupName = nameof(Texts.ReelSpinEffectName), Name = nameof(Texts.ReelSpinTile), Description = nameof(Texts.ReelSpinTile), Order = 30, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Tile { get => tile; set => Set(ref tile, value); }
        bool tile = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            //AviUtl側に対応するスクリプトが無いため出力しない
            yield break;
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new ReelSpinEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Rotation, Direction, Blur];
    }
}
