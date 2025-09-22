namespace YukkuriMovieMaker.Plugin.Community.Voice.LivetoonTTS
{
    internal class LivetoonTTSSettings : SettingsBase<LivetoonTTSSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "Livetoon TTS";

        //TODO:
        //クローズドベータ期間中は本番環境での利用が推奨されていないため、設定画面は非表示にしている。
        //正式サービス開始後にtrueに変更する。
#if DEBUG
        public override bool HasSettingView => true;
#else
        public override bool HasSettingView => false;
#endif

        public override object? SettingView => new LivetoonTTSSettingsView();

        string apiKey = string.Empty;
        public string APIKey { get=> apiKey; set=>Set(ref apiKey, value); }

        public override void Initialize()
        {

        }
    }
}