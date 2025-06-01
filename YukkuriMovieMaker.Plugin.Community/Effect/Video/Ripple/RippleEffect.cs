using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Ripple
{
    [VideoEffect(nameof(Texts.RippleEffectName), [VideoEffectCategories.Animation,], ["リップル", "ripple"], ResourceType = typeof(Texts))]
    public class RippleEffect : VideoEffectBase
    {
        public override string Label => Texts.RippleEffectName;

        [Display(GroupName = nameof(Texts.RippleGroupName), Name = nameof(Texts.RippleEffectXName), Description = nameof(Texts.RippleEffectXDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500d, 500d)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RippleGroupName), Name = nameof(Texts.RippleEffectYName), Description = nameof(Texts.RippleEffectYDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500d, 500d)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RippleGroupName), Name = nameof(Texts.RippleEffectAmplitudeName), Description = nameof(Texts.RippleEffectAmplitudeDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -100d, 100d)]
        public Animation Amplitude { get; } = new Animation(15, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RippleGroupName), Name = nameof(Texts.RippleEffectWaveLengthName), Description = nameof(Texts.RippleEffectWaveLengthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -100d, 100d)]
        public Animation WaveLength { get; } = new Animation(60, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.RippleGroupName), Name = nameof(Texts.RippleEffectPeriodName), Description = nameof(Texts.RippleEffectPeriodDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), -1d, 1d, ResourceType = typeof(Texts))]
        public Animation Period { get; } = new Animation(0.5d, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={X.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Y.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={WaveLength.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Amplitude.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=波紋（設定）@YMM4\r\n" +
                $"param=\r\n";

            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Period.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=波紋（描画）@YMM4\r\n" +
                $"param=\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new RippleEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            X,
            Y,
            Amplitude,
            WaveLength,
            Period
        ];
    }
}
