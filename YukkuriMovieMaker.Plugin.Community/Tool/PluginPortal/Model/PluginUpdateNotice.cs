using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal sealed record PluginUpdateNotice(string Name, string PluginUrl, PluginVersion CurrentVersion, PluginVersion LatestVersion)
    {
        public string VersionText =>
            $"{PluginUpdateChecker.FormatVersion(CurrentVersion)} → {PluginUpdateChecker.FormatVersion(LatestVersion)}";

        public string NotificationKey => $"{PluginUrl}|{PluginUpdateChecker.FormatVersion(LatestVersion)}";
    }
}
