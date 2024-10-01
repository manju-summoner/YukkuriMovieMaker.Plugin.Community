using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorCorrection
{

    [VideoEffect(nameof(Texts.ColorCorrectionEffectName), [VideoEffectCategories.Filtering], ["Color adjustment", "明るさ", "コントラスト", "色相", "輝度", "彩度", "lightness", "contrast", "hue", "brightness", "saturation"], ResourceType = typeof(Texts))]
    public class ColorCorrectionEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.ColorCorrectionEffectName} L{Lightness.GetValue(0, 1, 30):F0}, C{Contrast.GetValue(0, 1, 30):F0}, H{HueRotation.GetValue(0, 1, 30):F0}, B{Brightness.GetValue(0, 1, 30):F0}, S{Saturation.GetValue(0, 1, 30):F0}";

        [Display(GroupName = nameof(Texts.ColorCorrectionGroupName), Name = nameof(Texts.ColorCorrectionEffectLightnessName), Description = nameof(Texts.ColorCorrectionEffectLightnessDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 200d)]
        public Animation Lightness { get; } = new Animation(100, 0, 200);

        [Display(GroupName = nameof(Texts.ColorCorrectionGroupName), Name = nameof(Texts.ColorCorrectionEffectContrastName), Description = nameof(Texts.ColorCorrectionEffectContrastDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 200d)]
        public Animation Contrast { get; } = new Animation(100, 0, 200);


        [Display(GroupName = nameof(Texts.ColorCorrectionGroupName), Name = nameof(Texts.ColorCorrectionEffectHueRotationName), Description = nameof(Texts.ColorCorrectionEffectHueRotationDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360d, 360d)]
        public Animation HueRotation { get; } = new Animation(0, -3600, 3600);

        [Display(GroupName = nameof(Texts.ColorCorrectionGroupName), Name = nameof(Texts.ColorCorrectionEffectBrightnessName), Description = nameof(Texts.ColorCorrectionEffectBrightnessDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 200d)]
        public Animation Brightness { get; } = new Animation(100, 0, 200);

        [Display(GroupName = nameof(Texts.ColorCorrectionGroupName), Name = nameof(Texts.ColorCorrectionEffectSaturationName), Description = nameof(Texts.ColorCorrectionEffectSaturationDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0d, 200d)]
        public Animation Saturation { get; } = new Animation(100, 0, 200);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Lightness, Contrast, HueRotation, Brightness, Saturation];

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            yield return $"_name=色調補正\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"明るさ={Lightness.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"ｺﾝﾄﾗｽﾄ={Contrast.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"色相={HueRotation.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"輝度={Brightness.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"彩度={Saturation.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"飽和する=0\r\n";
        }
        public override Player.Video.IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ColorCorrectionEffectProcessor(devices, this);
        }


    }
}
