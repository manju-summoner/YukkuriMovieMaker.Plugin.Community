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

        [Display(GroupName = nameof(Texts.TilingGroupItemsEffectName), Name = nameof(Texts.Columns), Description = nameof(Texts.Columns), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 1, 5)]
        public Animation Columns { get; } = new Animation(1, 1, 100000);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Columns.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グループ制御中のアイテムで画面分割@YMM4\r\n" +
                $"param=" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new TilingGroupItemsEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Columns];
    }
}
