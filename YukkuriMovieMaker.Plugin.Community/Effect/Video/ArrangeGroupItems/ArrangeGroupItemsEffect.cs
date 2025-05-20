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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ArrangeGroupItems
{
    [VideoEffect(nameof(Texts.ArrangeGroupItemsEffectName), [VideoEffectCategories.Layout], ["整列", "Arrange items in group control at equal intervals"], IsEffectItemSupported = false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]

    internal class ArrangeGroupItemsEffect : VideoEffectBase
    {
        public override string Label => Texts.ArrangeGroupItemsEffectName;

        [Display(GroupName = nameof(Texts.ArrangeGroupItemsEffectName), Name = nameof(Texts.Interval), Description = nameof(Texts.Interval), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation Interval { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ArrangeGroupItemsEffectName), Name = nameof(Texts.Angle), Description = nameof(Texts.Angle), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Interval.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Angle.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グループ制御中のアイテムを等間隔に配置@YMM4\r\n" +
                $"param=" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ArrangeGroupItemsEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Interval, Angle];
    }
}
