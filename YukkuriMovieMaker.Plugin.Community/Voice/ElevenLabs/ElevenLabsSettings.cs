using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsSettings : SettingsBase<ElevenLabsSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "ElevenLabs";

        public override bool HasSettingView => true;

        public override object? SettingView => new ElevenLabsSettingsView();

        public string APIKey { get => apiKey; set => Set(ref apiKey, value); }
        string apiKey = string.Empty;

        public string? TTSModel { get => ttsModel; set => Set(ref ttsModel, value); }
        string? ttsModel = null;

        /// <summary>
        /// 話者一覧に出す声。手入力の行と、ボイス一覧 API から選んで足した行が混ざる。
        /// 選んで足した行を設定画面の表にすぐ出すため、変更を通知するコレクションにしている
        /// </summary>
        public ObservableCollection<ElevenLabsVoice> Voices { get; } = [];

        public override void Initialize()
        {

        }
    }
}
