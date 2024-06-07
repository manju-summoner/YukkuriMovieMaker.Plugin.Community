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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuminanceKey
{
    [VideoEffect(nameof(Texts.LuminanceKeyEffectName), [VideoEffectCategories.Composition], ["luminance key"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class LuminanceKeyEffect : VideoEffectBase
    {
        public override string Label => Texts.LuminanceKeyEffectName;

        [Display(GroupName = nameof(Texts.LuminanceKeyEffectName), Name = nameof(Texts.Threshold), Description = nameof(Texts.Threshold), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Threshold { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.LuminanceKeyEffectName), Name = nameof(Texts.Smoothness), Description = nameof(Texts.Smoothness), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Smoothness { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.LuminanceKeyEffectName), Name = nameof(Texts.Mode), Description = nameof(Texts.Mode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public LuminanceKeyEffectMode Mode { get=>mode; set => Set(ref mode, value); }
        private LuminanceKeyEffectMode mode;

        [Display(GroupName = nameof(Texts.LuminanceKeyEffectName), Name = nameof(Texts.IsInvert), Description = nameof(Texts.IsInvert), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInvert { get => isInvert; set => Set(ref isInvert, value); }
        private bool isInvert;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Threshold.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Smoothness.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=ルミナンスキー@YMM4-未実装\r\n" +
                $"param=" +
                    $"local {(IsInvert ? 1 : 0)};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new LuminanceKeyEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Threshold, Smoothness];
    }
}
