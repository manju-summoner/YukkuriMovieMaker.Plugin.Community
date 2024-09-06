using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Wave
{
    [VideoEffect(nameof(Texts.WaveEffectName), [VideoEffectCategories.Animation], ["raster scroll", "ラスター", "縦波", "横波", "vertical wave", "horizontal wave", "波打つ"], ResourceType = typeof(Texts))]
    public class WaveEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.WaveEffectName}";

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectWaveAngle1), Description = nameof(Texts.WaveEffectWaveAngle1), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle1 { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectWaveAngle2), Description = nameof(Texts.WaveEffectWaveAngle2), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle2 { get; } = new Animation(90, -36000, 36000);


        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectAmplitudeName), Description = nameof(Texts.WaveEffectAmplitudeDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 500d)]
        public Animation Amplitude { get; } = new Animation(100, 0, 99999);

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectWaveLengthName), Description = nameof(Texts.WaveEffectWaveLengthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 500d)]
        public Animation WaveLength { get; } = new Animation(100, 0, 99999);

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectPeriodName), Description = nameof(Texts.WaveEffectPeriodDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), -1d, 1d, ResourceType = typeof(Texts))]
        public Animation Period { get; } = new Animation(0.5, -1000, 1000);


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            yield return $"_name=ラスター\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"横幅={Amplitude.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"高さ={WaveLength.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"周期={Period.ToExoString(keyFrameIndex, "F2", fps, x => x * 4)}\r\n" +
                $"縦ラスター={(Angle1.Values[0].Value is 0 ? 0 : 1)}\r\n" +
                $"ランダム振幅=0\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new WaveEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [
            Angle1,
            Angle2,
            Amplitude,
            WaveLength,
            Period
        ];

        #region 旧API
        [Obsolete("Angle1を使用してください")]
        public WaveDirection WaveDirection
        {
            set
            {
                if (value is WaveDirection.Horizontal)
                    Angle1.Values[0].Value = 90;
                else if (value is WaveDirection.Vertical)
                    Angle1.Values[0].Value = 0;
            }
        }
        #endregion
    }
}
