using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TilingGroupItems
{
    [VideoEffect(nameof(Texts.TilingGroupItemsEffectName), [VideoEffectCategories.Layout], ["整列", "Tiling items in group control"], IsEffectItemSupported = false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]

    internal class TilingGroupItemsEffect : VideoEffectBase
    {
        public override string Label => Texts.TilingGroupItemsEffectName;

        [Display(GroupName = nameof(Texts.TilingGroupItemsEffectName), Name = nameof(Texts.Wrap), Description = nameof(Texts.WrapDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 5)]
        public Animation Wrap { get; } = new Animation(1, 1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TilingGroupItemsEffectName), Name = nameof(Texts.Vertical), Description = nameof(Texts.VerticalDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsVertical { get => isVertical; set=>Set(ref isVertical, value); }
        bool isVertical;

        [Display(GroupName = nameof(Texts.TilingGroupItemsEffectName), Name = nameof(Texts.EndAligned), Description = nameof(Texts.EndAlignedDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsEndAligned { get => isEndAligned; set => Set(ref isEndAligned, value); }
        bool isEndAligned;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Wrap.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グループ制御中のアイテムで画面分割@YMM4\r\n" +
                $"param=" +
                    "local isVertical=" + (IsVertical ? 1 : 0) + ";\r\n" +
                    "local isEndAligned=" + (IsEndAligned ? 1 : 0) + ";\r\n" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new TilingGroupItemsEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Wrap];
    }
}
