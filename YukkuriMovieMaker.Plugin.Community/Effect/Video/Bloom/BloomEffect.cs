using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Bloom
{
    [VideoEffect(nameof(Texts.Bloom), [VideoEffectCategories.Filtering], ["ブルーム", "bloom", "グロー", "glow"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class BloomEffect : VideoEffectBase
    {
        public override string Label => Texts.Bloom;

        [Display(GroupName = nameof(Texts.Bloom), Name = nameof(Texts.Strength), Description = nameof(Texts.StrengthDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Strength { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.Bloom), Name = nameof(Texts.Threshold), Description = nameof(Texts.StrengthDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Threshold { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.Bloom), Name = nameof(Texts.Blur), Description = nameof(Texts.StrengthDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 250)]
        public Animation Blur { get; } = new Animation(50, 0, 250);

        [Display(GroupName = nameof(Texts.Bloom), Name = nameof(Texts.IsFixedSizeEnabled), Description = nameof(Texts.IsFixedSizeEnabledDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsFixedSizeEnabled { get => isFiexedSizeEnabled; set => Set(ref isFiexedSizeEnabled, value); }
        bool isFiexedSizeEnabled = false;

        [Display(GroupName = nameof(Texts.Bloom), Name = nameof(Texts.IsColorizationEnabled), Description = nameof(Texts.StrengthDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsColorizationEnabled { get=> isColorizationEnabled; set => Set(ref isColorizationEnabled, value); }
        bool isColorizationEnabled = false;

        [Display(GroupName = nameof(Texts.Bloom), Name = nameof(Texts.Color), Description = nameof(Texts.StrengthDesc), ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(IsColorizationEnabled), true)]
        public Color Color { get=> color; set => Set(ref color, value); }
        Color color = Colors.White;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Strength.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Threshold.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={Blur.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=発光@YMM4-未実装\r\n" +
                $"param=" +
                    $"local isColorizationEnabled={(IsColorizationEnabled?1:0)};" +
                    $"local color={Color.R:X2}{Color.G:X2}{Color.B:X2};" +
                    $"\r\n";
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new BloomEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Strength, Threshold, Blur];
    }
}
