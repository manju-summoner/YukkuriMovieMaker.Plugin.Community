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
        /// <summary>
        /// 旧プロジェクトの波長を現在の波長へ換算する係数。
        /// 旧シェーダーは投影距離が sqrt(2) 倍に膨らんだうえで 2*pi を掛けていなかったため、
        /// 実際の周期が指定値の pi*sqrt(2) 倍になっていた。
        /// </summary>
        static readonly double LegacyWaveLengthScale = Math.PI * Math.Sqrt(2);

        public override string Label => $"{Texts.WaveEffectName}";

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectWaveAngle1), Description = nameof(Texts.WaveEffectWaveAngle1), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle1 { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectWaveAngle2), Description = nameof(Texts.WaveEffectWaveAngle2), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle2 { get; } = new Animation(90, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);


        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectAmplitudeName), Description = nameof(Texts.WaveEffectAmplitudeDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 500d)]
        public Animation Amplitude { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectWaveLengthName), Description = nameof(Texts.WaveEffectWaveLengthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 500d)]
        public Animation WaveLength2 { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.WaveGroupName), Name = nameof(Texts.WaveEffectPeriodName), Description = nameof(Texts.WaveEffectPeriodDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.SecUnit), -1d, 1d, ResourceType = typeof(Texts))]
        public Animation Period { get; } = new Animation(0.5, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            yield return $"_name=ラスター\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"横幅={Amplitude.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"高さ={WaveLength2.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
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
            WaveLength2,
            Period
        ];

        #region 旧API
        /// <summary>
        /// 旧プロジェクトファイル互換用のアクセサ。
        /// 波長の実周期が指定値の pi*sqrt(2) 倍だった頃のファイルはこのプロパティを持つので、
        /// 読み込み時に <see cref="WaveLength2"/> へ換算して引き継ぐ。
        /// getterを付けると2つの理由で壊れる。Json.NETは既定のObjectCreationHandling.Autoだと
        /// getterのある参照型プロパティをsetterを呼ばずにその場でpopulateするので換算が走らず、
        /// さらにこのプロパティが保存対象に入って WaveLength2 より後ろに出力されるため、
        /// 新しいファイルを読むときに読み込み済みの WaveLength2 を上書きしてしまう。
        /// </summary>
        [Obsolete("WaveLength2を使用してください")]
        public Animation WaveLength
        {
            set
            {
                WaveLength2.CopyFrom(value);
                //Animation.MultiplyToEachValuesは移動量指定のとき先頭の値しか掛けないため、
                //全体を定数倍したいここでは使えない。
                foreach (var animationValue in WaveLength2.Values)
                    animationValue.Value *= LegacyWaveLengthScale;
            }
        }

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
