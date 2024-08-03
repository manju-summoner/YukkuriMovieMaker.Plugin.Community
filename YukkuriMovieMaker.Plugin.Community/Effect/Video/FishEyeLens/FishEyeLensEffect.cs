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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.FishEyeLens
{
    [VideoEffect(nameof(Texts.FishEyeLens), [VideoEffectCategories.Filtering], ["fish eye lens", "レンズ歪み", "歪曲収差", "レンズディストーション", "lens distortion", "レンズ補正", "lens correction"], IsAviUtlSupported = false, IsEffectItemSupported = false, ResourceType = typeof(Texts))]
    internal class FishEyeLensEffect : VideoEffectBase
    {
        public override string Label => Texts.FishEyeLens;

        [Display(GroupName = nameof(Texts.FishEyeLens), Name = nameof(Texts.Projection), Description = nameof(Texts.Projection), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ProjectionMode Projection { get => projection; set => Set(ref projection, value); }
        ProjectionMode projection = ProjectionMode.Stereographic;

        [Display(GroupName = nameof(Texts.FishEyeLens), Name = nameof(Texts.Angle), Description = nameof(Texts.AngleOfView), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180, 180)]
        public Animation Angle { get; set; } = new Animation(0, -180, 180);

        [Display(GroupName = nameof(Texts.FishEyeLens), Name = nameof(Texts.Zoom), Description = nameof(Texts.Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Zoom { get; } = new Animation(100, 0, 5000);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Angle.ToExoString(keyFrameIndex, "F0", fps)}\r\n" +
                $"track1={Zoom.ToExoString(keyFrameIndex, "F0", fps)}\r\n" +
                $"name=レンズ歪み@YMM4-未実装\r\n" +
                $"param=";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new FishEyeLensProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Angle, Zoom];
    }
}
