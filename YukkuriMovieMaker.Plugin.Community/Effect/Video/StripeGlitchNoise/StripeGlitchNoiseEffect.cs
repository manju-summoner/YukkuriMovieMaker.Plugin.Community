using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.StripeGlitchNoise
{
    [VideoEffect(nameof(Texts.StripeGlitchNoiseEffectName), [VideoEffectCategories.Animation], ["glitch noise"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class StripeGlitchNoiseEffect : VideoEffectBase
    {
        public override string Label => Texts.StripeGlitchNoiseEffectName;

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectStripeCountName), Description = nameof(Texts.StripeGlitchNoiseEffectStripeCountDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", nameof(Texts.UnitCount), 0, 10, ResourceType = typeof(Texts))]
        public Animation StripeCount { get; } = new Animation(10, 0, 1000);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectStripeMaxWidthName), Description = nameof(Texts.StripeGlitchNoiseEffectStripeMaxWidthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation StripeMaxWidth { get; } = new Animation(100, 0, 4000);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectStripeMaxShiftName), Description = nameof(Texts.StripeGlitchNoiseEffectStripeMaxShiftDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation StripeMaxShift { get; } = new Animation(100, 0, 4000);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectColorMaxShiftName), Description = nameof(Texts.StripeGlitchNoiseEffectColorMaxShiftDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 50)]
        public Animation ColorMaxShift { get; } = new Animation(5, 0, 4000);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectPlaybackRateName), Description = nameof(Texts.StripeGlitchNoiseEffectPlaybackRateDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation PlaybackRate { get; } = new Animation(30, 0, 100);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectProbability), Description = nameof(Texts.StripeGlitchNoiseEffectProbabilityDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Probability { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectName), Name = nameof(Texts.StripeGlitchNoiseEffectIsHardBorderModeName), Description = nameof(Texts.StripeGlitchNoiseEffectIsHardBorderModeDesc), Order = 100, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsHardBorderMode { get => isHardBorderMode; set => Set(ref isHardBorderMode, value); }
        bool isHardBorderMode = false;

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectComplicationGroupName), Name = nameof(Texts.StripeGlitchNoiseEffectRepeatName), Description = nameof(Texts.StripeGlitchNoiseEffectRepeatDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 0, 5)]
        public Animation Repeat { get; } = new Animation(3, 0, 32);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectComplicationGroupName), Name = nameof(Texts.StripeGlitchNoiseEffectStripeMaxWidthAttenuationName), Description = nameof(Texts.StripeGlitchNoiseEffectStripeMaxWidthAttenuationDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation StripeMaxWidthAttenuation { get; } = new Animation(10, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.StripeGlitchNoiseEffectComplicationGroupName), Name = nameof(Texts.StripeGlitchNoiseEffectStripeMaxShiftAttenuationName), Description = nameof(Texts.StripeGlitchNoiseEffectStripeMaxShiftAttenuationDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation StripeMaxShiftAttenuation { get; } = new Animation(50, 0, YMM4Constants.VeryLargeValue);


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={StripeCount.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={StripeMaxWidth.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={StripeMaxShift.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Repeat.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グリッチノイズ（帯1）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local isHardBorder={(IsHardBorderMode ? 1 : 0)};\r\n";
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={ColorMaxShift.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=グリッチノイズ（帯2）@YMM4-未実装\r\n" +
                $"param=\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new StripeGlitchNoiseEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [StripeCount, StripeMaxWidth, StripeMaxShift, ColorMaxShift, PlaybackRate, Probability, Repeat, StripeMaxWidthAttenuation, StripeMaxShiftAttenuation];
    }
}
