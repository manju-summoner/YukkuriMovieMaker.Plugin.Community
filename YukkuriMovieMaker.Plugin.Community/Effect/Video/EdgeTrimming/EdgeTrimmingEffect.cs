using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeTrimming
{
    [VideoEffect(nameof(Texts.EdgeTrimmingEffectName), [nameof(VideoEffectCategories.Filtering)], ["edge trimming"], IsEffectItemSupported = false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class EdgeTrimmingEffect : VideoEffectBase
    {
        public override string Label => Texts.EdgeTrimmingEffectName;


        [Display(GroupName = nameof(Texts.EdgeTrimmingEffectName), Name = nameof(Texts.ThicknessName), Description = nameof(Texts.ThicknessDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 10)]
        public Animation Thickness { get; } = new Animation(10, 0, 500);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Thickness.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=縁削り@YMM4-未実装\r\n" +
                $"param=\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new EdgeTrimmingEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Thickness];
    }
}
