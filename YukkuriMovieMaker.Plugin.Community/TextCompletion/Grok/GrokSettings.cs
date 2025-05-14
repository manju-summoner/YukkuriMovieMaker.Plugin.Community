using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.Grok
{
    internal class GrokSettings : SettingsBase<GrokSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => "Grok";

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        string? apiKey;
        bool isSendImageEnabled = false;

        public string? ApiKey { get => apiKey; set => Set(ref apiKey, value); }
        public bool IsSendImageEnabled { get => isSendImageEnabled; set => Set(ref isSendImageEnabled, value); }


        public GrokRequestSettings RequestSettings { get; } = new GrokRequestSettings();

        public override void Initialize()
        {

        }
    }
}
