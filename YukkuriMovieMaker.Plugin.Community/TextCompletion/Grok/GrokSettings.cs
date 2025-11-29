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

        public GrokRequestSettings RequestSettings { get; } = new GrokRequestSettings();

        public override void Initialize()
        {

        }

        #region 古いAPI
        [Obsolete]

        public string? ApiKey { set => RequestSettings.GeneralSettings.ApiKey = value ?? string.Empty; }
        [Obsolete]
        public bool IsSendImageEnabled { set => RequestSettings.GeneralSettings.IsSendImageEnabled = value; }
        #endregion
    }
}
