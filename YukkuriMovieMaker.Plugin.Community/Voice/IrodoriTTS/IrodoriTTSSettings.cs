using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSSettings : SettingsBase<IrodoriTTSSettings>
{
    public override SettingsCategory Category => SettingsCategory.Voice;
    public override string Name => "Irodori-TTS";
    public override bool HasSettingView => true;
    public override object? SettingView => new IrodoriTTSSettingsView();

    public string TTSUrl { get => field; set => Set(ref field, value); } = string.Empty;
    public string VoiceDesignUrl { get => field; set => Set(ref field, value); } = string.Empty;
    public string GradioAppPath { get => field; set => Set(ref field, value); } = string.Empty;
    public int ServerPort { get => field; set => Set(ref field, value); } = 7860;

    // VoiceDesign の前回設定（話者名以外）
    public string LastCaption { get => field; set => Set(ref field, value); } = string.Empty;
    public string LastSpeechText { get => field; set => Set(ref field, value); } = string.Empty;
    public string LastSeed { get => field; set => Set(ref field, value); } = string.Empty;
    public double LastNumSteps { get => field; set => Set(ref field, value); } = 40;
    public string LastVoiceDesignCheckpoint { get => field; set => Set(ref field, value); } = string.Empty;

    public override void Initialize() { }
}
