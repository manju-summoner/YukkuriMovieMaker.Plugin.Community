using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.SpiralTransform
{
    [VideoEffect(nameof(Texts.SpiralTransformEffectName), [VideoEffectCategories.Filtering], ["渦巻き", "螺旋", "spiral"], true, false, ResourceType = typeof(Texts))]
    public class SpiralTransformEffect : VideoEffectBase
    {
        public override string Label => Texts.SpiralTransformEffectName;

        [Display(GroupName = nameof(Texts.SpiralTransformEffectName), Name = nameof(Texts.SpiralTransformEffectAngleName), Description = nameof(Texts.SpiralTransformEffectAngleDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(360, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.SpiralTransformEffectName), Name = nameof(Texts.SpiralTransformEffectIsRotateOuterName), Description = nameof(Texts.SpiralTransformEffectIsRotateOuterDesc), Order = 100, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsRotateOuter { get => isRotateOuter; set => Set(ref isRotateOuter, value); }
        bool isRotateOuter = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"name=渦巻き変換@YMM4-未実装\r\n" +
                $"param=" +
                    $"local angle={Angle.ToExoString(keyFrameIndex, "F0", fps)};" +
                    $"local isRotateOuter={(IsRotateOuter ? 1 : 0)};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new SpiralTransformEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Angle];
    }
}
