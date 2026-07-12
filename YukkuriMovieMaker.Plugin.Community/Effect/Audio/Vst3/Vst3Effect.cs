using System.ComponentModel.DataAnnotations;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
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

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new Vst3EffectProcessor(this);
        }

        internal event Action<uint, double>? ParameterEdited;

        internal void NotifyParameterEdited(uint parameterId, double normalizedValue)
        {
            ParameterEdited?.Invoke(parameterId, normalizedValue);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Mix];

        async void UpdateHasEditor()
        {
            var path = filePath;
            var result = await Vst3EditorProbe.HasEditorAsync(path);
            if (path != filePath || hasEditor == result)
                return;
            hasEditor = result;
            OnPropertyChanged(nameof(HasEditor));
        }
    }
}
