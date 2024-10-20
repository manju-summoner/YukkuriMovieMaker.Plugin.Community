using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.RectangleGlitchNoise
{
    [VideoEffect(nameof(Texts.RectangleGlitchNoise), [VideoEffectCategories.Animation], ["glitch noise"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class RectangleGlitchNoiseEffect : VideoEffectBase
    {
        public override string Label => Texts.RectangleGlitchNoise;

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.RectangleCount), Description = nameof(Texts.RectangleCount), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", nameof(Texts.UnitCount), 0, 10, ResourceType = typeof(Texts))]
        public Animation RectangleCount { get; } = new Animation(10, 0, 1000);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.RectangleMaxWidth), Description = nameof(Texts.RectangleMaxWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation RectangleMaxWidth { get; } = new Animation(100, 0, 4000);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.RectangleMaxHeight), Description = nameof(Texts.RectangleMaxHeight), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation RectangleMaxHeight { get; } = new Animation(50, 0, 4000);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.RectangleMaxXShift), Description = nameof(Texts.RectangleMaxXShift), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation RectangleMaxXShift { get; } = new Animation(100, 0, 4000);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.RectangleMaxYShift), Description = nameof(Texts.RectangleMaxYShift), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation RectangleMaxYShift { get; } = new Animation(50, 0, 4000);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.ColorMaxShift), Description = nameof(Texts.ColorMaxShift), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 50)]
        public Animation ColorMaxShift { get; } = new Animation(5, 0, 4000);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.PlaybackRate), Description = nameof(Texts.PlaybackRate), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation PlaybackRate { get; } = new Animation(30, 0, 100);

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.IsClipping), Description = nameof(Texts.IsClipping), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsClipping { get => isClipping; set => Set(ref isClipping, value); }
        bool isClipping = true;

        [Display(GroupName = nameof(Texts.RectangleGlitchNoise), Name = nameof(Texts.IsHardBorderMode), Description = nameof(Texts.IsHardBorderMode), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsHardBorderMode { get => isHardBorderMode; set => Set(ref isHardBorderMode, value); }
        bool isHardBorderMode = false;

        [Display(GroupName = nameof(Texts.ComplicationGroupName), Name = nameof(Texts.Repeat), Description = nameof(Texts.Repeat), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 0, 5)]
        public Animation Repeat { get; } = new Animation(3, 0, 32);

        [Display(GroupName = nameof(Texts.ComplicationGroupName), Name = nameof(Texts.RectangleMaxWidthAttenuation), Description = nameof(Texts.RectangleMaxWidthAttenuation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation RectangleMaxWidthAttenuation { get; } = new Animation(10, 0, int.MaxValue);

        [Display(GroupName = nameof(Texts.ComplicationGroupName), Name = nameof(Texts.RectangleMaxHeightAttenuation), Description = nameof(Texts.RectangleMaxHeightAttenuation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation RectangleMaxHeightAttenuation { get; } = new Animation(10, 0, int.MaxValue);

        [Display(GroupName = nameof(Texts.ComplicationGroupName), Name = nameof(Texts.RectangleMaxXShiftAttenuation), Description = nameof(Texts.RectangleMaxXShiftAttenuation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation RectangleMaxXShiftAttenuation { get; } = new Animation(50, 0, int.MaxValue);

        [Display(GroupName = nameof(Texts.ComplicationGroupName), Name = nameof(Texts.RectangleMaxYShiftAttenuation), Description = nameof(Texts.RectangleMaxYShiftAttenuation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation RectangleMaxYShiftAttenuation { get; } = new Animation(50, 0, int.MaxValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={RectangleCount.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={RectangleMaxWidth.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={RectangleMaxHeight.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={RectangleMaxXShift.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グリッチノイズ（四角1）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local isClipping={(IsClipping ? 1 : 0)};\r\n" +
                    $"local isHardBorder={(IsHardBorderMode ? 1 : 0)};\r\n";
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={RectangleMaxYShift.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={ColorMaxShift.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={PlaybackRate.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Repeat.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グリッチノイズ（四角2）@YMM4-未実装\r\n" +
                $"param=\r\n";
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={RectangleMaxWidthAttenuation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={RectangleMaxHeightAttenuation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={RectangleMaxXShiftAttenuation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={RectangleMaxYShiftAttenuation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グリッチノイズ（四角3）@YMM4-未実装\r\n" +
                $"param=\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new RectangleGlitchNoiseEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [RectangleCount, RectangleMaxWidth, RectangleMaxHeight, RectangleMaxXShift, RectangleMaxYShift, ColorMaxShift, PlaybackRate, Repeat, RectangleMaxWidthAttenuation, RectangleMaxHeightAttenuation, RectangleMaxXShiftAttenuation, RectangleMaxYShiftAttenuation];
    }
}
