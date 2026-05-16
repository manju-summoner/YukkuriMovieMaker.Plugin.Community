using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.TextPaste.TextPaste_Enum;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.TextPaste
{
    [VideoEffect(nameof(Texts.TextPasteEffectDefaultName), [VideoEffectCategories.Animation], ["Paste Text", "テキスト貼り付け", "シャッフル"], isAviUtlSupported: false, ResourceType = typeof(Texts))]
    internal class TextPasteEffect : VideoEffectBase
    {
        public override string Label => Texts.TextPasteEffectDefaultName;

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Draw), Name = nameof(Texts.TextPasteEffectDisplayName_X), Description = nameof(Texts.TextPasteEffectDiscription_X), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Pixels), -1000.0, 1000.0, ResourceType = typeof(Texts))]
        public Animation X { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Draw), Name = nameof(Texts.TextPasteEffectDisplayName_Y), Description = nameof(Texts.TextPasteEffectDiscription_Y), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Pixels), -1000.0, 1000.0, ResourceType = typeof(Texts))]
        public Animation Y { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Draw), Name = nameof(Texts.TextPasteEffectDisplayName_Z), Description = nameof(Texts.TextPasteEffectDiscription_Z), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Pixels), -1000.0, 1000.0, ResourceType = typeof(Texts))]
        public Animation Z { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Draw), Name = nameof(Texts.TextPasteEffectDisplayName_Opacity), Description = nameof(Texts.TextPasteEffectDiscription_Opacity), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Percent), 0.0, 100.0, ResourceType = typeof(Texts))]
        public Animation Opacity { get; } = new Animation(100.0, 0.0, 100.0);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Draw), Name = nameof(Texts.TextPasteEffectDisplayName_Zoom), Description = nameof(Texts.TextPasteEffectDiscription_Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Percent), 0.0, 200.0, ResourceType = typeof(Texts))]
        public Animation Zoom { get; } = new Animation(100.0, 0.0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Draw), Name = nameof(Texts.TextPasteEffectDisplayName_Rotation), Description = nameof(Texts.TextPasteEffectDiscription_Rotation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Degrees), -360.0, 360.0, ResourceType = typeof(Texts))]
        public Animation Rotation { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Display), Name = nameof(Texts.TextPasteEffectDisplayName_DisplayMode), Description = nameof(Texts.TextPasteEffectDiscription_DisplayMode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public TextDisplayMode DisplayMode { get => field; set => Set(ref field, value); }
            = TextDisplayMode.Overlay;

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Display), Name = nameof(Texts.TextPasteEffectDisplayName_BasePoint), Description = nameof(Texts.TextPasteEffectDiscription_BasePoint), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public BasePoint BasePoint { get => field; set => Set(ref field, value); }
            = BasePoint.CenterCenter;

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Text), Name = nameof(Texts.TextPasteEffectDisplayName_Text), Description = nameof(Texts.TextPasteEffectDiscription_Text), ResourceType = typeof(Texts))]
        [RichTextEditor(DecorationPropertyName = nameof(Decorations), FontPropertyName = nameof(Font), ForegroundPropertyName = nameof(Color))]
        public string Text { get => field; set => Set(ref field, value); }
            = string.Empty;

        public ImmutableList<TextDecoration> Decorations { get => field; set => Set(ref field, value); }
            = ImmutableList<TextDecoration>.Empty;

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Text), Name = nameof(Texts.TextPasteEffectDisplayName_Font), Description = nameof(Texts.TextPasteEffectDiscription_Font), ResourceType = typeof(Texts))]
        [FontComboBox]
        public string Font { get => field; set => Set(ref field, value); }
            = "メイリオ";

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Text), Name = nameof(Texts.TextPasteEffectDisplayName_Size), Description = nameof(Texts.TextPasteEffectDiscription_Size), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Pixels), 1.0, 100.0, ResourceType = typeof(Texts))]
        public Animation FontSize { get; } = new Animation(34.0, 1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Text), Name = nameof(Texts.TextPasteEffectDisplayName_Color), Description = nameof(Texts.TextPasteEffectDiscription_Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => field; set => Set(ref field, value); }
            = Colors.White;

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Text), Name = nameof(Texts.TextPasteEffectDisplayName_CharSpacing), Description = nameof(Texts.TextPasteEffectDiscription_CharSpacing), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Pixels), -50.0, 50.0, ResourceType = typeof(Texts))]
        public Animation CharSpacing { get; } = new Animation(0, -YMM4Constants.VeryLargeValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.TextPasteEffectGroupName_Text), Name = nameof(Texts.TextPasteEffectDisplayName_LineHeight), Description = nameof(Texts.TextPasteEffectDiscription_LineHeight), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", nameof(Texts.TextPasteEffectUnit_Percent), 0.0, 300.0, ResourceType = typeof(Texts))]
        public Animation LineHeight { get; } = new Animation(100.0, 0.0, YMM4Constants.VeryLargeValue);

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
            => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new TextPasteEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [X, Y, Z, Opacity, Zoom, Rotation, FontSize, CharSpacing, LineHeight];
    }
}
