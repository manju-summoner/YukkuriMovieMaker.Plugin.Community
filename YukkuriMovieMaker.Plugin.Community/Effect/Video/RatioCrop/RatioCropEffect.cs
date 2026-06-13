using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RatioCrop
{
    [VideoEffect(nameof(Texts.RatioCrop), [VideoEffectCategories.Composition], ["clipping", "crop", "クロップ", "ratio", "rate", "%", "割合"], isAviUtlSupported: false, ResourceType = typeof(Texts))]
    internal class RatioCropEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.RatioCrop} 上{Top.GetValue(0, 1, 30):F0}%, 下{Bottom.GetValue(0, 1, 30):F0}%, 左{Left.GetValue(0, 1, 30):F0}%, 右{Right.GetValue(0, 1, 30):F0}%";

        [Display(GroupName = nameof(Texts.RatioCrop), Name = nameof(Texts.Top), Description = nameof(Texts.Top), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Top { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.RatioCrop), Name = nameof(Texts.Bottom), Description = nameof(Texts.Bottom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Bottom { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.RatioCrop), Name = nameof(Texts.Left), Description = nameof(Texts.Left), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Left { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.RatioCrop), Name = nameof(Texts.Right), Description = nameof(Texts.Right), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Right { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.RatioCrop), Name = nameof(Texts.IsCentering), Description = nameof(Texts.IsCentering), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsCentering { get => isCentering; set => Set(ref isCentering, value); }
        bool isCentering = true;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Top.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Bottom.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={Left.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Right.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=クリッピング（比率）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local isCentering={(IsCentering ? 1 : 0)};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new RatioCropEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Top, Bottom, Left, Right];
    }
}