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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RadialArrangeGroupItems
{
    [VideoEffect(nameof(Texts.RadialArrangeGroupItemsEffectName), [VideoEffectCategories.Layout], ["整列", "Radial arrange items in group control at equal intervals"], IsEffectItemSupported = false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]

    internal class RadialArrangeGroupItemsEffect : VideoEffectBase
    {
        public override string Label => Texts.RadialArrangeGroupItemsEffectName;

        [Display(GroupName = nameof(Texts.RadialArrangeGroupItemsEffectName), Name = nameof(Texts.Radius), Description = nameof(Texts.Radius), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -100, 100)]
        public Animation Radius { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RadialArrangeGroupItemsEffectName), Name = nameof(Texts.Arc), Description = nameof(Texts.Arc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        public Animation Arc { get; } = new Animation(360, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RadialArrangeGroupItemsEffectName), Name = nameof(Texts.Direction), Description = nameof(Texts.Direction), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Direction { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RadialArrangeGroupItemsEffectName), Name = nameof(Texts.SyncAngle), Description = nameof(Texts.SyncAngle), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsRotationSynchronized { get => isRotationSynchronized; set => Set(ref isRotationSynchronized, value); }
        bool isRotationSynchronized = false;

        [Display(GroupName = nameof(Texts.RadialArrangeGroupItemsEffectName), Name = nameof(Texts.Centering), Description = nameof(Texts.Centering), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsCentering { get=> isCentering; set => Set(ref isCentering, value); }
        bool isCentering = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Radius.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Arc.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={Direction.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グループ制御中のアイテムを円周上に均等配置@YMM4\r\n" +
                $"param=" +
                    $"local isRotationSynchronized={(IsRotationSynchronized ? 1 : 0)};" +
                    $"local isCentering={(IsCentering ? 1 : 0)}" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new RadialArrangeGroupItemsEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Radius, Arc, Direction];
    }
}
