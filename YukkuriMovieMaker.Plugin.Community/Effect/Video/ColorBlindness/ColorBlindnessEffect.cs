using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ColorBlindness
{
    [VideoEffect(nameof(Texts.ColorBlindness), [VideoEffectCategories.Filtering], ["色覚異常", "色弱", "色盲", "色覚多様性","色神異常", "Color blindness",], ResourceType = typeof(Texts))]
    public class ColorBlindnessEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.ColorBlindness}";

        [Display(GroupName = nameof(Texts.ColorBlindness), Name = nameof(Texts.ColorBlindnessType), Description = nameof(Texts.ColorBlindnessType), Order = 100, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ColorBlindnessType Type { get => type; set => Set(ref type, value); }
        ColorBlindnessType type = ColorBlindnessType.P;

        [Display(GroupName = nameof(Texts.ColorBlindness), Name = nameof(Texts.Strength), Description = nameof(Texts.Strength), Order = 200, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(100, 0, 100);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength];

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={(int)Type}\r\n" +
                $"track1={Strength.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=色覚異常@YMM4-未実装\r\n" +
                $"param=" +
                    $"\r\n";
        }
        public override Player.Video.IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ColorBlindnessEffectProcessor(devices, this);
        }
    }
}
