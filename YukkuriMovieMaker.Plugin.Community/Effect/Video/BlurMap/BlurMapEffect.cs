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
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.BlurMap
{
    [VideoEffect(nameof(Texts.BlurMap), [VideoEffectCategories.Filtering], ["ブラーマップ", "Blur Map",], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class BlurMapEffect : VideoEffectBase
    {
        public override string Label => Texts.BlurMap;

        [Display(GroupName = nameof(Texts.BlurMap), Name = nameof(Texts.Blur), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation Blur { get; } = new Animation(50, 0, 750);

        [Display(GroupName = nameof(Texts.BlurMap), Name = nameof(Texts.IsFixedSize), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsFixedSize { get => isFixedSize; set => Set(ref isFixedSize, value); }
        bool isFixedSize = false;

        [Display(GroupName = nameof(Texts.BlurMap), ResourceType = typeof(Texts), AutoGenerateField = true)]
        public Plugin.Brush.Brush Brush { get; } = new Plugin.Brush.Brush();


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Blur.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=ぼかしマップ@YMM4-未実装\r\n" +
                $"param=local isFixedSize={(IsFixedSize?1:0)};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new BlurMapProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Blur, Brush];
    }
}
