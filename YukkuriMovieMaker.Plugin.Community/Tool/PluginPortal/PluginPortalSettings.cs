using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalSettings : SettingsBase<PluginPortalSettings>
    {
        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => Texts.PluginPortal;

        public override bool HasSettingView => true;
        public override object? SettingView => new PluginPortalSettingsView();

        public bool IsCleanYmmeFile { get=> isCleanYmmeFile; set => Set(ref isCleanYmmeFile, value); }
        private bool isCleanYmmeFile = true;

        public string YmmeFilePath { get => ymmeFilePath; set => Set(ref ymmeFilePath, value); }
        private string ymmeFilePath = Path.Combine(AppDirectories.UserDirectory, @"ymmes");

        public override void Initialize()
        {
        }
    }
}
