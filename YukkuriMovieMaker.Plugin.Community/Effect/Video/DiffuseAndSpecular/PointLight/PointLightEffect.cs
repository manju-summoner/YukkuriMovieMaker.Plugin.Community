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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular.PointLight
{
    [VideoEffect(nameof(Texts.PointLight), [VideoEffectCategories.Decoration], ["点光源", "ポイントライト", "point light", "鏡面反射", "拡散反射", "diffuse", "specular", "ディフューズ", "スペキュラー"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]

    internal class PointLightEffect : DiffuseAndSpecularEffectBase
    {
        [Display(GroupName = nameof(Texts.Light), Name = nameof(Texts.XName), Description = nameof(Texts.XDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Light), Name = nameof(Texts.YName), Description = nameof(Texts.YDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Light), Name = nameof(Texts.ZName), Description = nameof(Texts.ZDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new PointLightEffectProcessor(devices, this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            foreach (var filter in base.CreateExoVideoFilters(keyFrameIndex, exoOutputDescription))
                yield return filter;

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={X.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Y.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={Z.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=点光源（描画）@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => base.GetAnimatables().Concat([X,Y,Z]);
    }
}
