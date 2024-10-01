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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.UnidirectionalBlur
{
    [VideoEffect(nameof(Texts.UnidirectionalBlur), [VideoEffectCategories.Filtering], ["unidirectional blur", "ブラー", "ぼかし"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class UnidirectionalBlurEffect : VideoEffectBase
    {
        public override string Label => Texts.UnidirectionalBlur;

        [Display(GroupName = nameof(Texts.UnidirectionalBlur), Name = nameof(Texts.Angle), Description = nameof(Texts.Angle), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new(0, -36000,36000);

        [Display(GroupName = nameof(Texts.UnidirectionalBlur), Name = nameof(Texts.Length), Description = nameof(Texts.Length), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation Length { get; } = new Animation(10, 0, 2000);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Angle.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Length.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=片方向ブラー@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new UnidirectionalBlurProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Angle, Length];
    }
}
