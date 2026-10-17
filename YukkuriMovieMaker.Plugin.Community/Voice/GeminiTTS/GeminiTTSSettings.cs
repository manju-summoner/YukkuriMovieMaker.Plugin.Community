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
        /// ボイス一覧 API から選んだ声。プリセット30種は含めない（常に話者一覧に出る）
        /// </summary>
        public ObservableCollection<GeminiTTSVoiceInfo> Voices { get; } = [];


        public override void Initialize()
        {

        }
    }
}
