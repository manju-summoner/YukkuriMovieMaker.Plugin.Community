using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalSettings : SettingsBase<PluginPortalSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => Texts.PluginPortal;

        public override bool HasSettingView => false;
        public override object? SettingView => throw new NotImplementedException();

        public List<string> InstalledPlugins { get=> installedPlugins; set => Set(ref installedPlugins, value); }
        private List<string> installedPlugins = [];

        public override void Initialize()
        {
        }
    }
}
