using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3ホスティングの設定。追加のプラグイン検索フォルダーを保持する
    /// </summary>
    internal class Vst3Settings : SettingsBase<Vst3Settings>
    {
        public override SettingsCategory Category => SettingsCategory.AudioEffect;

        public override string Name => "VST3";

        public override bool HasSettingView => true;

        public override object SettingView => new Vst3SettingsView();

        /// <summary>
        /// 標準フォルダーに加えてVST3プラグインを検索するフォルダー
        /// </summary>
        public string[] AdditionalPluginDirectories { get => additionalPluginDirectories; set => Set(ref additionalPluginDirectories, value); }
        string[] additionalPluginDirectories = [];

        public override void Initialize()
        {
            additionalPluginDirectories ??= [];
        }
    }
}
