using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal sealed record PluginUpdateStatus(
        PluginVersion? InstalledVersion,
        PluginVersion? LatestVersion,
        string? PageUrl,
        bool HasGitHubRelease = false,
        IPluginUpdateInfo? DownloadableUpdate = null,
        bool IsLatestUpdateChecked = false)
    {
        public bool HasUpdate =>
            InstalledVersion is not null
            && LatestVersion is not null
            && LatestVersion.CompareTo(InstalledVersion) > 0;

        public string? InstalledVersionText => InstalledVersion is null ? null : PluginUpdateChecker.FormatVersion(InstalledVersion);
        public string? LatestVersionText => LatestVersion is null ? null : PluginUpdateChecker.FormatVersion(LatestVersion);
    }
}
