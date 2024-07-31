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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LensBlur
{
    [VideoEffect(nameof(Texts.LensBlur), [VideoEffectCategories.Filtering], ["lens blur", "レンズブラー"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class LensBlurEffect : VideoEffectBase
    {
        public override string Label => Texts.LensBlur;

        [Display(GroupName = nameof(Texts.LensBlur), Name = nameof(Texts.BlurRadius), Description = nameof(Texts.BlurRadius), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation BlurRadius { get; } = new Animation(10, 0, 2000);

        [Display(GroupName = nameof(Texts.LensBlur), Name = nameof(Texts.Brightness), Description = nameof(Texts.Brightness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Brightness { get; } = new Animation(100, 0, 100_00);

        [Display(GroupName = nameof(Texts.LensBlur), Name = nameof(Texts.EdgeStrength), Description = nameof(Texts.EdgeStrength), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", 0, 2)]
        public Animation EdgeStrength { get; } = new Animation(2, 0, 10);

        [Display(GroupName = nameof(Texts.LensBlur), Name = nameof(Texts.Quality), Description = nameof(Texts.QualityDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", 0.5, 16)]
        public Animation Quality { get; } = new Animation(16, 0.5, 100);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={BlurRadius.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Brightness.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={EdgeStrength.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track3={Quality.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=レンズぼかし@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new LensBlurProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [BlurRadius, Brightness, EdgeStrength, Quality];
    }
}
