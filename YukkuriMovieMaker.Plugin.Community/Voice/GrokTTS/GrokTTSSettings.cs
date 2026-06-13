using System.Collections.ObjectModel;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GrokTTS
{
    internal class GrokTTSSettings : SettingsBase<GrokTTSSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "Grok TTS";

        public override bool HasSettingView => true;

        public override object? SettingView => new GrokTTSSettingsView();

        string apiKey = string.Empty;

        public string ApiKey { get => apiKey; set => Set(ref apiKey, value); }

        public ObservableCollection<GrokTTSVoice> Voices { get; } = [];

        public override void Initialize()
        {
        }
    }
}
