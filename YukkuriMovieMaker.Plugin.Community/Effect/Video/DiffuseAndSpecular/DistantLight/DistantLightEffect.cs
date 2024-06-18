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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular.DistantLight
{
    [VideoEffect(nameof(Texts.DistantLight), [VideoEffectCategories.Decoration], ["遠方光源", "平行光源","遠方ライト", "平行ライト", "ディスタントライト", "distant light", "鏡面反射", "拡散反射", "diffuse", "specular", "ディフューズ", "スペキュラー"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]

    internal class DistantLightEffect : DiffuseAndSpecularEffectBase
    {
        [Display(GroupName = nameof(Texts.Light), Name = nameof(Texts.Azimuth), Description = nameof(Texts.Azimuth), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Azimuth { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Light), Name = nameof(Texts.Elevation), Description = nameof(Texts.Elevation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Elevation { get; } = new Animation(0, -36000, 36000);


        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new DistantLightEffectProcessor(devices, this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            foreach (var filter in base.CreateExoVideoFilters(keyFrameIndex, exoOutputDescription))
                yield return filter;

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Azimuth.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Elevation.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=平行光源（描画）@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([Azimuth, Elevation]);
    }
}
