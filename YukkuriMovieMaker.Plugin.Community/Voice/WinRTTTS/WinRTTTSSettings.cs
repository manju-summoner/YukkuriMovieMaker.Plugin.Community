using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Voice.WinRTTTS
{
    internal class WinRTTTSSettings : SettingsBase<WinRTTTSSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;
        public override string Name => "WinRT TTS";
        public override bool HasSettingView => false;
        public override object SettingView => throw new NotImplementedException();

        public override void Initialize() { }
    }
}
