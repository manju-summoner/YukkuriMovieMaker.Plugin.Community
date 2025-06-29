using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VignetteBlur
{
    [VideoEffect(nameof(Texts.VignetteBlur), [VideoEffectCategories.Filtering], ["周辺ぼかし", "周辺減光", "周辺増光", "周辺色ずれ", "色収差", "ガウスぼかし", "ガウスブラー", "ガウシアンブラー", "回転ぼかし", "回転ブラー", "放射ぼかし", "放射ブラー", "チルトシフト", "ビネット", "ブラー", "Tilt Shift", "Vignette", "Blur", "Color Shift", "Chromatic Aberration", "Gaussian Blur", "Radial Blur", "Circular Blur"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class VignetteBlurEffect : VideoEffectBase
    {
        public override string Label => Texts.VignetteBlur;

        [Display(GroupName = nameof(Texts.Area), Name = nameof(Texts.X), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.Area), Name = nameof(Texts.Y), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.Area), Name = nameof(Texts.Radius), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Radius { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.Area), Name = nameof(Texts.Aspect), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", -100, 100)]
        public Animation Aspect { get; } = new Animation(0, -100, 100);

        [Display(GroupName = nameof(Texts.Area), Name = nameof(Texts.Softness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        public Animation Softness { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.Area), Name = nameof(Texts.FixedSize), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsFixedSize { get => isFixedSize; set => Set(ref isFixedSize, value); }
        private bool isFixedSize = false;


        [Display(GroupName = nameof(Texts.VignetteBlur), Name = nameof(Texts.BlurMode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public VignetBlurMode Mode { get => mode; set => Set(ref mode, value); }
        VignetBlurMode mode = VignetBlurMode.Gaussian;

        [Display(GroupName = nameof(Texts.VignetteBlur), Name = nameof(Texts.Blur), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        [ShowPropertyEditorWhen(nameof(Mode), VignetBlurMode.Gaussian | VignetBlurMode.Radial)]
        public Animation Blur { get; } = new Animation(50, 0, 750);//ガウスぼかしのStandardDeviationの最大値250 * 3 = 750

        [Display(GroupName = nameof(Texts.VignetteBlur), Name = nameof(Texts.Blur), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0, 360)]
        [ShowPropertyEditorWhen(nameof(Mode), VignetBlurMode.Circular)]
        public Animation BlurAngle { get; } = new Animation(10, 0, 750);

        [Display(GroupName = nameof(Texts.VignetteBlur), Name = nameof(Texts.Lightness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Lightness { get; } = new Animation(100, 0, 200);

        [Display(GroupName = nameof(Texts.VignetteBlur), Name = nameof(Texts.ColorShift), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", -100, 100)]
        public Animation ColorShift { get; } = new Animation(0, -250, 250);//4096 / 750 / 2 = 2.73


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={X.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Y.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={Radius.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Aspect.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=周辺ぼけ減光1@YMM4-未実装\r\n" +
                $"param=" +
                    $"local isFixedSize = {(isFixedSize ? 1 : 0)};" +
                    $"\r\n";
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Softness.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={(mode is VignetBlurMode.Circular ? BlurAngle.ToExoString(keyFrameIndex, "F2", fps) : Blur.ToExoString(keyFrameIndex, "F2", fps))}\r\n" +
                $"track2={Lightness.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={ColorShift.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=周辺ぼけ減光2@YMM4-未実装\r\n" +
                $"param=" +
                    $"local mode = {Mode};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new VignetteBlurProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Radius, Aspect, Softness, Blur, BlurAngle, Lightness, ColorShift];
    }
}
