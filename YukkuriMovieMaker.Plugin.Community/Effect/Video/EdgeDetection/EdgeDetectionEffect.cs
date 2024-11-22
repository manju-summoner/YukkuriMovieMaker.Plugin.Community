using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.EdgeDetection
{
    [VideoEffect(nameof(Texts.EdgeDetectionEffectName), [VideoEffectCategories.Filtering], ["エッジ抽出", "エッジ検出", "edge detection", "edge extraction", "線画", "輪郭"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class EdgeDetectionEffect : VideoEffectBase
    {
        public override string Label => Texts.EdgeDetectionEffectName;

        [Display(GroupName = nameof(Texts.EdgeDetectionEffectName), Name = nameof(Texts.EdgeDetectionEffectStrengthName), Description = nameof(Texts.EdgeDetectionEffectStrengthDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.EdgeDetectionEffectName), Name = nameof(Texts.EdgeDetectionEffectBlurRadiusName), Description = nameof(Texts.EdgeDetectionEffectBlurRadiusDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 5)]
        public Animation BlurRadius { get; } = new Animation(0, 0, 100);

        [Display(GroupName = nameof(Texts.EdgeDetectionEffectName), Name = nameof(Texts.EdgeDetectionEffectModeName), Description = nameof(Texts.EdgeDetectionEffectModeDesc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EdgeDetectionMode Mode { get => mode; set => Set(ref mode, value); }
        EdgeDetectionMode mode = EdgeDetectionMode.Sobel;

        [Display(GroupName = nameof(Texts.EdgeDetectionEffectName), Name = nameof(Texts.EdgeDetectionEffectIsOverlayEdgesName), Description = nameof(Texts.EdgeDetectionEffectIsOverlayEdgesDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsOverlayEdges { get => isOverlayEdges; set => Set(ref isOverlayEdges, value); }
        bool isOverlayEdges = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Strength.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={BlurRadius.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=エッジ抽出@YMM4-未実装\r\n" +
                $"param=" +
                    $"local mode={(int)mode:F0};" +
                    $"local isOverlayEdges={(isOverlayEdges ? 1 : 0)};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new EdgeDetectionEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength, BlurRadius];
    }
}
