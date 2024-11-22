using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using System.ComponentModel;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.LongShadow
{
    [VideoEffect(nameof(Texts.LongShadowEffectName), [VideoEffectCategories.Decoration], ["伸びる影", "ロングシャドー", "フラットシャドー", "long shadow", "flat shadow"], false, IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class LongShadowEffect : VideoEffectBase
    {
        public override string Label => $"{Texts.LongShadowEffectName}";

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectAngleName), Description = nameof(Texts.LongShadowEffectAngleDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Angle { get; } = new Animation(135, -3600, 3600, 360);

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectLengthName), Description = nameof(Texts.LongShadowEffectLengthDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 100)]
        public Animation Length { get; } = new Animation(50, 0, 99999);

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectOpacityName), Description = nameof(Texts.LongShadowEffectOpacityDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectAttenuationName), Description = nameof(Texts.LongShadowEffectAttenuationDesc), Order = 100, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Attenuation { get; } = new Animation(50, 0, 100);

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectShadowTypeName), Description = nameof(Texts.LongShadowEffectShadowTypeDesc), Order = 100, ResourceType = typeof(Texts))]
        [EnumComboBox]
        [DefaultValue(LongShadowType.Solid)]
        public LongShadowType ShadowType { get => shadowType; set => Set(ref shadowType, value); }
        LongShadowType shadowType = LongShadowType.Solid;

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectColor1Name), Description = nameof(Texts.LongShadowEffectColor1Desc), Order = 100, ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(ShadowType), LongShadowType.Solid | LongShadowType.Gradient)]
        public System.Windows.Media.Color Color1 { set => Set(ref color1, value, nameof(Color1), nameof(Label)); get => color1; }
        System.Windows.Media.Color color1 = System.Windows.Media.Colors.White;

        [Display(GroupName = nameof(Texts.LongShadowEffectName), Name = nameof(Texts.LongShadowEffectColor2Name), Description = nameof(Texts.LongShadowEffectColor2Desc), Order = 100, ResourceType = typeof(Texts))]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(ShadowType), LongShadowType.Gradient)]
        public System.Windows.Media.Color Color2 { set => Set(ref color2, value, nameof(Color2), nameof(Label)); get => color2; }
        System.Windows.Media.Color color2 = System.Windows.Media.Colors.Black;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return
                $"_name=アニメーション効果\r\n" +
                $"_disable={(IsEnabled ? 0 : 1)}\r\n" +
                $"track0={Angle.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Length.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={Opacity.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Attenuation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=伸びる影@YMM4-未実装\r\n" +
                $"param=" +
                    $"local shadowType={(int)shadowType};" +
                    $"local color1=0x{Color1.R:X2}{Color1.G:X2}{Color1.B:X2};" +
                    $"local color2=0x{Color2.R:X2}{Color2.G:X2}{Color2.B:X2};" +
                    $"\r\n";
        }

        public override Player.Video.IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new LongShadowEffectProcessor(devices, this);
        }
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Angle, Length, Opacity, Attenuation];
    }
}
