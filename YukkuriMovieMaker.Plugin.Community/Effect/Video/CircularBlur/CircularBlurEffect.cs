using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.CircularBlur
{
    [VideoEffect(nameof(Texts.CircularBlurEffectName), [nameof(Texts.EffectCategoryFilteringName),], ["circular blur"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class CircularBlurEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.CircularBlurEffectName} {Angle.GetValue(0, 1, 30):F0}°";

        [Display(GroupName = nameof(Texts.CircularBlurGroupName), Name = nameof(Texts.CircularBlurEffectAngleName), Description = nameof(Texts.CircularBlurEffectAngleDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0d, 360d)]
        public Animation Angle { get; } = new Animation(10, 0, 360);

        [Display(GroupName = nameof(Texts.CircularBlurGroupName), Name = nameof(Texts.CircularBlurEffectXName), Description = nameof(Texts.CircularBlurEffectXDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500d, 500d)]
        public Animation X { get; } = new Animation(0, -99999, 99999);

        [Display(GroupName = nameof(Texts.CircularBlurGroupName), Name = nameof(Texts.CircularBlurEffectYName), Description = nameof(Texts.CircularBlurEffectYDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500d, 500d)]
        public Animation Y { get; } = new Animation(0, -99999, 99999);

        [Display(GroupName = nameof(Texts.CircularBlurGroupName), Name = nameof(Texts.CircularBlurEffectIsHardBorderModeName), Description = nameof(Texts.CircularBlurEffectIsHardBorderModeDesc), Order = 100, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsHardBorderMode
        {
            set { Set(ref isHardBorderMode, value); }
            get { return isHardBorderMode; }
        }
        bool isHardBorderMode = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;

            yield return $"_name=放射ブラー\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"範囲={Angle.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"X={X.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"Y={Y.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"サイズを固定={(IsHardBorderMode ? 1 : 0)}\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new CircularBlurEffectProcessor(devices, this);
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Angle, X, Y];
    }
}
