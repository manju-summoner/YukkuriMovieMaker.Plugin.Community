using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSSettings : SettingsBase<IrodoriTTSSettings>
{
    public override SettingsCategory Category => SettingsCategory.Voice;
    public override string Name => "Irodori-TTS";
    public override bool HasSettingView => true;
    public override object? SettingView => new IrodoriTTSSettingsView();

    public string TTSUrl { get; set => Set(ref field, value); } = string.Empty;
    public string VoiceDesignUrl { get; set => Set(ref field, value); } = string.Empty;
    public string GradioAppPath { get; set => Set(ref field, value); } = string.Empty;
    public int ServerPort { get; set => Set(ref field, value); } = 7860;
    public bool ShowConsoleWindow { get; set => Set(ref field, value); } = true;

    // VoiceDesign の前回設定（話者名以外）
    public string LastCaption { get; set => Set(ref field, value); } = string.Empty;
    public string LastSpeechText { get; set => Set(ref field, value); } = string.Empty;
    public string LastSeed { get; set => Set(ref field, value); } = string.Empty;
    public double LastNumSteps { get; set => Set(ref field, value); } = 40;
    public string LastVoiceDesignCheckpoint { get; set => Set(ref field, value); } = string.Empty;

    public override void Initialize() { }
}
