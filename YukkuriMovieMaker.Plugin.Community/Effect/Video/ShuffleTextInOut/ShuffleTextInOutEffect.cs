using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleTextInOut.ShffleTextInOut_Enum;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleTextInOut
{
    [VideoEffect(nameof(Texts.ShuffleTextEffectDefaultName), [VideoEffectCategories.Transition], ["Shuffle Text InOut","シャッフル", "Shuffle Letters InOut"], isAviUtlSupported: false, ResourceType =typeof(Texts))]
    internal class ShuffleTextInOutEffect : VideoEffectBase
    {
        public override string Label
        {
            get
            {
                if (EffectEnter && !EffectExit)
                    return Texts.ShuffleTextEffectEnterName;
                if (!EffectEnter && EffectExit)
                    return Texts.ShuffleTextEffectExitName;
                if (!EffectEnter && !EffectExit)
                    return Texts.ShuffleTextEffectName;
                return Texts.ShuffleTextEffectDefaultName;
            }
        }

        //登場退場
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_Appearing), Description = nameof(Texts.ShuffleTextEffectDiscription_Appearing), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool EffectEnter
        {
            get => effectEnter;
            set => Set(ref effectEnter, value, nameof(EffectEnter), nameof(Label));
        }
        bool effectEnter = true;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_Exit), Description = nameof(Texts.ShuffleTextEffectDiscription_Exit), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool EffectExit
        {
            get => effectExit;
            set => Set(ref effectExit, value, nameof(EffectExit), nameof(Label));
        }
        bool effectExit = true;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_Duration), Description = nameof(Texts.ShuffleTextEffectDiscription_Duration), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.ShuffleTextEffectUnit_Seconds), 0, 0.5, ResourceType = typeof(Texts))]
        [DefaultValue(0d)]
        [Range(0d, YMM4Constants.VeryLargeValue)]
        public double T { get => time; set => Set(ref time, value); }
        double time = 0.30;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_Interval), Description = nameof(Texts.ShuffleTextEffectDiscription_Interval), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", nameof(Texts.ShuffleTextEffectUnit_Seconds), 0, 0.25, ResourceType = typeof(Texts))]
        public Animation Interval { get; } = new Animation(0, 0, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_Delay), Description = nameof(Texts.ShuffleTextEffectDiscription_Delay), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Delay { get => delay; set => Set(ref delay, value); }
        bool delay = true;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_DisplayMode), Description = nameof(Texts.ShuffleTextEffectDiscription_DisplayMode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public DisplayMode Enum_DisplayMode { get => mode_displayEnum; set => Set(ref mode_displayEnum, value); }
        DisplayMode mode_displayEnum = DisplayMode.Nomal;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_StartTime), Description = nameof(Texts.ShuffleTextEffectDiscription_StartTime), ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.ShuffleTextEffectUnit_Seconds), 0, 0.5, ResourceType = typeof(Texts))]
        [DefaultValue(0d)]
        [Range(0d, YMM4Constants.VeryLargeValue)]
        [ShowPropertyEditorWhen(nameof(Enum_DisplayMode), DisplayMode.Order)]
        public double DisplayStartTime { get => startTime; set => Set(ref startTime, value); }
        double startTime = 0.30;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AppearanceAndExit), Name = nameof(Texts.ShuffleTextEffectDisplayName_FromBack), Description = nameof(Texts.ShuffleTextEffectDiscription_FromBack), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_DisplayMode), DisplayMode.Order)]
        public bool Back { get => back; set => Set(ref back, value); }
        bool back = false;

        //アニメーションテキスト
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_DisplayText), Description = nameof(Texts.ShuffleTextEffectDiscription_DisplayText), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public TextMode Enum_Mode { get => mode_Enum; set => Set(ref mode_Enum, value); }
        TextMode mode_Enum = TextMode.Alphabet;

        //Custom
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Alphabet_Upper), Description = nameof(Texts.ShuffleTextEffectDiscription_Alphabet_Upper), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool UpLetter { get => upletter; set => Set(ref upletter, value); }
        bool upletter = false;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Alphabet_Lower), Description = nameof(Texts.ShuffleTextEffectDiscription_Alphabet_Lower), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool LowLetter { get => lowletter; set => Set(ref lowletter, value); }
        bool lowletter = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Hirakana), Description = nameof(Texts.ShuffleTextEffectDiscription_Hirakana), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool Hirakana { get => hirakana; set => Set(ref hirakana, value); }
        bool hirakana = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Katakana), Description = nameof(Texts.ShuffleTextEffectDiscription_Katakana), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool Katakana { get => katakana; set => Set(ref katakana, value); }
        bool katakana = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Kanji), Description = nameof(Texts.ShuffleTextEffectDiscription_Kanji), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool Kanji { get => kanji; set => Set(ref kanji, value); }
        bool kanji = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Number), Description = nameof(Texts.ShuffleTextEffectDiscription_Number), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool Number { get => num; set => Set(ref num, value); }
        bool num = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Symbol), Description = nameof(Texts.ShuffleTextEffectDiscription_Symbol), ResourceType = typeof(Texts))]
        [ToggleSlider]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public bool Symbol { get => symbol; set => Set(ref symbol, value); }
        bool symbol = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Text), Description = nameof(Texts.ShuffleTextEffectDiscription_Text), ResourceType = typeof(Texts))]
        [TextEditor(AcceptsReturn = true)]
        [ShowPropertyEditorWhen(nameof(Enum_Mode), TextMode.Custom)]
        public string Text { get => text; set => Set(ref text, value); }
        string text = string.Empty;


        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Font), Description = nameof(Texts.ShuffleTextEffectDiscription_Font), ResourceType = typeof(Texts))]
        [FontComboBox]
        public string Font { get => font; set => Set(ref font, value); }
        string font = "メイリオ";

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Size), Description = nameof(Texts.ShuffleTextEffectDiscription_Size), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 1.0, 50.0)]
        public Animation FontSize { get; } = new Animation(34.0, 1, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Color), Description = nameof(Texts.ShuffleTextEffectDiscription_Color), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Bold), Description = nameof(Texts.ShuffleTextEffectDiscription_Bold), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Bold { get => bold; set => Set(ref bold, value); }
        bool bold = false;
        [Display(GroupName = nameof(Texts.ShuffleTextEffectGroupName_AnimationText), Name = nameof(Texts.ShuffleTextEffectDisplayName_Italic), Description = nameof(Texts.ShuffleTextEffectDiscription_Italic), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool Italic { get => italic; set => Set(ref italic, value); }
        bool italic = false;


        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new ShuffleTextInOutEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Interval, FontSize];
    }
}
