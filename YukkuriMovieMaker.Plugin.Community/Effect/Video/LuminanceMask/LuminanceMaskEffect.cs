using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LuminanceMask
{
    [VideoEffect(nameof(Texts.LuminanceMask), [VideoEffectCategories.Layout], ["ルミナンスマスク", "luminance mask", "luminosity mask"], IsEffectItemSupported = false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]

    internal class LuminanceMaskEffect : VideoEffectBase
    {
        public override string Label => Texts.LuminanceMask;

        [Display(GroupName = nameof(Texts.LuminanceMask), Name = nameof(Texts.Blur), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation Blur { get; } = new Animation(0, 0, 750);

        [Display(GroupName = nameof(Texts.LuminanceMask), Name = nameof(Texts.InvertArea), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsInverted { get => isInverted; set => Set(ref isInverted, value); }
        bool isInverted = false;

        [Display(GroupName = nameof(Texts.LuminanceMask), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public Plugin.Brush.Brush Brush { get; } = new Plugin.Brush.Brush();


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {

            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Blur.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=輝度マスク@YMM4\r\n" +
                $"param=" +
                    $"local isInverted={(isInverted?1:0)};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new LuminanceMaskEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Blur, Brush];
    }
}
