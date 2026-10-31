using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSSettings : SettingsBase<GeminiTTSSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "GeminiTTS";

        public override bool HasSettingView => true;

        public override object? SettingView => new GeminiTTSSettingsView();


        string 
            apiKey = string.Empty,
            model = GeminiTTSModel.DefaultModelName;
        int tier = 1;

        public string ApiKey { get => apiKey; set => Set(ref apiKey, value); }
        public string Model { get => model; set => Set(ref model, value); }
        public int Tier { get => tier; set => Set(ref tier, value); }

        /// <summary>
        /// 手で入力した声。プリセット30種は入力しなくても常に話者一覧に出る。
        /// 削除ボタンで消した行を設定画面の表からすぐ消すため、変更を通知するコレクションにしている
        /// </summary>
        public ObservableCollection<GeminiTTSVoice> Voices { get; } = [];


        public override void Initialize()
        {

        }
    }
}
