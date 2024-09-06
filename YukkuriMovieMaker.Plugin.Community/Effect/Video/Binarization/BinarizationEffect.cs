using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Binarization
{
    [VideoEffect(nameof(Texts.BinarizationEffectName), [VideoEffectCategories.Filtering], ["二値化", "2値化", "Binarization", "threshold", "しきい値", "いき値", "閾値", "スレッショルド", "スレショルド"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class BinarizationEffect : VideoEffectBase
    {
        public override string Label => Texts.BinarizationEffectName;

        [Display(Name = nameof(Texts.BinarizationEffectThresholdName), Description = nameof(Texts.BinarizationEffectThresholdDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Threshold { get; } = new Animation(50, 0, 100);

        [Display(Name = nameof(Texts.BinarizationEffectIsInvertedName), Description = nameof(Texts.BinarizationEffectIsInvertedDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInverted { get => isInverted; set => Set(ref isInverted, value, nameof(IsInverted)); }
        bool isInverted = false;

        [Display(Name = nameof(Texts.BinarizationEffectKeepColorName), Description = nameof(Texts.BinarizationEffectKeepColorDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool KeepColor { get => keepColor; set => Set(ref keepColor, value, nameof(KeepColor)); }
        bool keepColor = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Threshold.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=二値化@YMM4\r\n" +
                $"param=" +
                    $"local isInverted={(IsInverted ? 1 : 0):F0};" +
                    $"local keepColor={(KeepColor ? 1 : 0):F0};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new BinarizationEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Threshold];
    }
}
