using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using System.ComponentModel;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ThreeDimensional
{
    [VideoEffect(nameof(Texts.ThreeDimensionalEffectName), [VideoEffectCategories.Decoration], ["立体化", "three dimensional"], false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class ThreeDimensionalEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.ThreeDimensionalEffectName}";

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectXName), Description = nameof(Texts.ThreeDimensionalEffectXDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, -99999, 99999);

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectYName), Description = nameof(Texts.ThreeDimensionalEffectYDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, -99999, 99999);

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectLengthName), Description = nameof(Texts.ThreeDimensionalEffectLengthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 50)]
        public Animation Length { get; } = new Animation(50, 0, 90);

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectOpacityName), Description = nameof(Texts.ThreeDimensionalEffectOpacityDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectAttenuationName), Description = nameof(Texts.ThreeDimensionalEffectAttenuationDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Attenuation { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectShadowTypeName), Description = nameof(Texts.ThreeDimensionalEffectShadowTypeDesc), Order = 100, ResourceType = typeof(Texts))]
        [EnumComboBox]
        [DefaultValue(ThreeDimensionalType.Solid)]
        public ThreeDimensionalType ShadowType { get => shadowType; set => Set(ref shadowType, value); }
        ThreeDimensionalType shadowType = ThreeDimensionalType.Solid;

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectColor1Name), Description = nameof(Texts.ThreeDimensionalEffectColor1Desc), Order = 100, ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(ShadowType), ThreeDimensionalType.Solid | ThreeDimensionalType.Gradient)]
        public System.Windows.Media.Color Color1 { set => Set(ref color1, value, nameof(Color1), nameof(Label)); get => color1; }
        System.Windows.Media.Color color1 = System.Windows.Media.Colors.White;

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectColor2Name), Description = nameof(Texts.ThreeDimensionalEffectColor2Desc), Order = 100, ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(ShadowType), ThreeDimensionalType.Gradient)]
        public System.Windows.Media.Color Color2 { set => Set(ref color2, value, nameof(Color2), nameof(Label)); get => color2; }
        System.Windows.Media.Color color2 = System.Windows.Media.Colors.Black;

        [Display(GroupName = nameof(Texts.ThreeDimensionalEffectName), Name = nameof(Texts.ThreeDimensionalEffectIsAbsolutePointName), Description = nameof(Texts.ThreeDimensionalEffectIsAbsolutePointDesc), Order = 100, ResourceType = typeof(Texts))]
        [ToggleSlider]
        [DefaultValue(false)]
        public bool IsAbsolutePoint { get => isAbsolutePoint; set => Set(ref isAbsolutePoint, value); }
        bool isAbsolutePoint = false;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={X.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track1={Y.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={Length.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"track2={Opacity.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=立体化（設定）@YMM4-未実装\r\n" +
                $"param=\r\n";
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Attenuation.ToExoString(keyFrameIndex, "F1", fps)}\r\n" +
                $"name=立体化（描画）@YMM4-未実装\r\n" +
                $"param=" +
                    $"local shadowType={(int)ShadowType};" +
                    $"local color1=0x{Color1.R:X2}{Color1.G:X2}{Color1.B:X2};" +
                    $"local color2=0x{Color2.R:X2}{Color2.G:X2}{Color2.B:X2};" +
                    $"local isAbsolutePoint={(IsAbsolutePoint ? 1 : 0)}" +
                    $"\r\n";
        }

        public override Player.Video.IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ThreeDimensionalEffectProcessor(devices, this);
        }
        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Length, Opacity, Attenuation];
    }
}
