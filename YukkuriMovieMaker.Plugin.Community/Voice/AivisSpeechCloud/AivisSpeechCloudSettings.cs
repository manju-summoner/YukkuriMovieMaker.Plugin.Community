using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud
{
    internal class AivisSpeechCloudSettings : SettingsBase<AivisSpeechCloudSettings>
    {
        public string ApiKey { get; set; } = string.Empty;

        public override SettingsCategory Category => SettingsCategory.Voice;

        public override string Name => "AivisSpeech Cloud";

        public override bool HasSettingView => true;

        public override object? SettingView => new AivisSpeechCloudSettingsView();

        public ObservableCollection<API.AivisSpeechCloudAPIModelInfo> Models { get; } = [];

        public override void Initialize()
        {
            if(Models.Count is 0)
            {
                foreach(var model in API.AivisSpeechCloudAPIModelInfo.GetDefaultModels())
                    Models.Add(model);
            }
        }
    }
}
