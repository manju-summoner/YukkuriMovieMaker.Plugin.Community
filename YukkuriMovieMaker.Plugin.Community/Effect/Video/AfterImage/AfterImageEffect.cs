using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.AfterImage
{
    [VideoEffect(nameof(Texts.AfterImage), [VideoEffectCategories.Decoration], ["残像", "afterimage", "モーションブラー", "motionblur"], false, ResourceType = typeof(Texts))]
    public sealed class AfterImageEffect : VideoEffectBase
    {
        public override string Label => Texts.AfterImage;

        [Display(GroupName = nameof(Texts.AfterImage), Name = nameof(Texts.Strength), Description = nameof(Texts.StrengthDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "％", 0, 100)]
        public Animation Strength { get; } = new(80, 0, 100);

        [Display(GroupName = nameof(Texts.AfterImage), Name = nameof(Texts.AfterImagePosition), Description = nameof(Texts.AfterImagePositionDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public AfterImagePosition Mode { get; set => Set(ref field, value); }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Strength.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=残像@YMM4\r\n" +
                $"param=" +
                    $"mode={Mode};\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AfterImageEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength];
    }
}
