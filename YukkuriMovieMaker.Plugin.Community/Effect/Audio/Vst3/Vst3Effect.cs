using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    [AudioEffect(nameof(Texts.Vst3Effect), [AudioEffectCategories.Effect], ["vst", "vst3", "plugin", "プラグイン"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class Vst3Effect : AudioEffectBase
    {
        public override string Label =>
            string.IsNullOrWhiteSpace(FilePath)
                ? Texts.Vst3Effect
                : $"{Texts.Vst3Effect} {Path.GetFileNameWithoutExtension(FilePath)}";

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.FilePathName), Description = nameof(Texts.FilePathDesc), ResourceType = typeof(Texts))]
        [Vst3FileSelector]
        public string FilePath
        {
            get => filePath;
            set
            {
                if (Set(ref filePath, value, nameof(FilePath), nameof(Label)))
                    UpdateHasEditor();
            }
        }
        string filePath = string.Empty;

        public bool HasEditor => hasEditor;
        bool hasEditor;

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.EditorName), Description = nameof(Texts.EditorDesc), ResourceType = typeof(Texts))]
        [OpenVst3EditorButton]
        [Vst3EditorVisible]
        public string PluginState { get => pluginState; set => Set(ref pluginState, value); }
        string pluginState = string.Empty;

        public string ControllerState { get => controllerState; set => Set(ref controllerState, value); }
        string controllerState = string.Empty;

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.MixName), Description = nameof(Texts.MixDesc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "%", 0, 100)]
        public Animation Mix { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.IsTempoSyncEnabledName), Description = nameof(Texts.IsTempoSyncEnabledDesc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsTempoSyncEnabled { get => isTempoSyncEnabled; set => Set(ref isTempoSyncEnabled, value); }
        bool isTempoSyncEnabled;

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.TempoName), Description = nameof(Texts.TempoDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F1", "BPM", 20d, 300d)]
        [DefaultValue(120d)]
        [Range(1d, 999d)]
        [ShowPropertyEditorWhen(nameof(IsTempoSyncEnabled), true)]
        public double Tempo { get => tempo; set => Set(ref tempo, Math.Clamp(value, 1, 999)); }
        double tempo = 120;

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.TimeSignatureNumeratorName), Description = nameof(Texts.TimeSignatureNumeratorDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1d, 16d)]
        [DefaultValue(4)]
        [Range(1, 64)]
        [ShowPropertyEditorWhen(nameof(IsTempoSyncEnabled), true)]
        public int TimeSignatureNumerator { get => timeSignatureNumerator; set => Set(ref timeSignatureNumerator, Math.Clamp(value, 1, 64)); }
        int timeSignatureNumerator = 4;

        [Display(GroupName = nameof(Texts.Vst3Effect), Name = nameof(Texts.TimeSignatureDenominatorName), Description = nameof(Texts.TimeSignatureDenominatorDesc), ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "", 1d, 16d)]
        [DefaultValue(4)]
        [Range(1, 64)]
        [ShowPropertyEditorWhen(nameof(IsTempoSyncEnabled), true)]
        public int TimeSignatureDenominator { get => timeSignatureDenominator; set => Set(ref timeSignatureDenominator, Math.Clamp(value, 1, 64)); }
        int timeSignatureDenominator = 4;

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new Vst3EffectProcessor(this);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Mix];

        internal void UpdateHasEditor()
        {
            var result = Vst3EditorProbe.GetHasEditor(filePath);
            if (hasEditor == result)
                return;
            hasEditor = result;
            OnPropertyChanged(nameof(HasEditor));
        }
    }
}
