using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Flip
{
    [VideoEffect(nameof(Texts.Flip), [VideoEffectCategories.Drawing], ["flip horizontal", "flip vertical", "左右反転", "上下反転", "上下左右", "ミラー", "mirror"], IsAviUtlSupported = false, IsEffectItemSupported = true, ResourceType = typeof(Texts))]
    internal class FlipEffect : VideoEffectBase
    {
        public override string Label => IsVertical && IsHorizontal ? Texts.Flip : IsHorizontal ? Texts.FlipHorizontal : IsVertical ? Texts.FlipVertical : Texts.Flip;

        [Display(GroupName = nameof(Texts.Flip), Name = nameof(Texts.FlipVertical), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsVertical { get; set => Set(ref field, value, nameof(IsVertical), nameof(Label)); } = false;

        [Display(GroupName = nameof(Texts.Flip), Name = nameof(Texts.FlipHorizontal), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsHorizontal { get; set => Set(ref field, value, nameof(IsHorizontal), nameof(Label)); } = true;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"name=上下左右反転@YMM4-未実装\r\n" +
                $"param=" +
                    $"local isHorizontal={(IsHorizontal ? 0 : 1)};" +
                    $"local isVertical={(IsVertical ? 0 : 1)};";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new FlipEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
