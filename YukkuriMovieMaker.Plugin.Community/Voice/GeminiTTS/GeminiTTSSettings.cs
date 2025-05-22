using Parlot.Fluent;
using System;
using System.Collections.Generic;
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
            model = GeminiTTSModels.DefaultModel;
        
        public string ApiKey { get => apiKey; set => Set(ref apiKey, value); }
        public string Model { get => model; set => Set(ref model, value); }


        public override void Initialize()
        {

        }
    }
}
