using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalSettings : SettingsBase<PluginPortalSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Tool;
        public override string Name => Texts.PluginPortal;

        public override bool HasSettingView => true;
        public override object? SettingView => new PluginPortalSettingsView();

        public bool IsCleanYmmeFile { get=> isCleanYmmeFile; set => Set(ref isCleanYmmeFile, value); }
        private bool isCleanYmmeFile = true;

        public string YmmeFilePath { get => ymmeFilePath; set => Set(ref ymmeFilePath, value); }
        private string ymmeFilePath = Path.Combine(AppDirectories.UserDirectory, @"ymmes");

        public bool IsCheckUpdatesOnStartup { get => isCheckUpdatesOnStartup; set => Set(ref isCheckUpdatesOnStartup, value); }
        private bool isCheckUpdatesOnStartup;

        public bool IsNotifyEveryStartup { get => isNotifyEveryStartup; set => Set(ref isNotifyEveryStartup, value); }
        private bool isNotifyEveryStartup = true;

        public string[] NotifiedVersions { get => notifiedVersions; set => Set(ref notifiedVersions, value); }
        private string[] notifiedVersions = [];

        public override void Initialize()
        {
        }
    }
}
