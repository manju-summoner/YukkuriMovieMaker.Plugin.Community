using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.TextCompletion.GoogleAI
{
    internal class GeminiTextCompletionSettings : SettingsBase<GeminiTextCompletionSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => throw new NotImplementedException();

        public override bool HasSettingView => false;

        public override object? SettingView => throw new NotImplementedException();

        public string APIKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-1.5-flash";
        public bool IsSendImageEnabled { get; set; } = false;

        public override void Initialize()
        {

        }
    }
}
