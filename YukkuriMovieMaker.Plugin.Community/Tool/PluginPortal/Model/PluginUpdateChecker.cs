using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal static partial class PluginUpdateChecker
    {
        private sealed record InstalledUpdater(string Name, IPluginUpdater Updater, string PluginUrl, PluginVersion? CurrentVersion);

        public static Dictionary<PluginCatalogItem, PluginUpdateStatus> CheckWithReleaseList(
            IEnumerable<PluginCatalogItem> catalog,
            IEnumerable<GitHubReleasesPluginSummary> releases,
            out List<(PluginCatalogItem Plugin, IPluginUpdater Updater)> otherSiteUpdaters)
        {
            var installedUpdaters = GetInstalledUpdaters();
            var latestReleases = GetLatestReleases(releases);

            var statuses = new Dictionary<PluginCatalogItem, PluginUpdateStatus>();
            otherSiteUpdaters = [];

            foreach (var plugin in catalog)
            {
                var pluginUrls = new[] { plugin.Url }
                    .Concat(plugin.Links ?? [])
                    .Select(NormalizeUrl)
                    .Where(url => url.Length > 0)
                    .ToHashSet();

                var gitHubUrl = FindGitHubRepositoryUrl(plugin);
                if (gitHubUrl is not null)
                {
                    if (!TryParseGitHubRepository(gitHubUrl, out var owner, out var repo))
                        continue;

                    var repository = ToRepositoryKey(owner, repo);
                    var installed = installedUpdaters.FirstOrDefault(u =>
                        pluginUrls.Contains(NormalizeUrl(u.PluginUrl))
                        || (TryParseGitHubRepository(u.PluginUrl, out var updaterOwner, out var updaterRepo)
                            && ToRepositoryKey(updaterOwner, updaterRepo) == repository));

                    statuses[plugin] = new PluginUpdateStatus(installed?.CurrentVersion, GetLatestVersion(latestReleases, repository), null);
                }
                else
                {
                    var installed = installedUpdaters.FirstOrDefault(u => pluginUrls.Contains(NormalizeUrl(u.PluginUrl)));
                    if (installed is null)
                        continue;

                    statuses[plugin] = new PluginUpdateStatus(installed.CurrentVersion, null, installed.PluginUrl);
                    otherSiteUpdaters.Add((plugin, installed.Updater));
                }
            }

            return statuses;
        }

        public static async Task<IReadOnlyList<PluginUpdateNotice>> CheckInstalledPluginsAsync()
        {
            var installedUpdaters = GetInstalledUpdaters();
            if (installedUpdaters.Count == 0)
                return [];

            var hasGitHubUpdater = installedUpdaters.Any(u => TryParseGitHubRepository(u.PluginUrl, out _, out _));
            var latestReleases = GetLatestReleases(hasGitHubUpdater ? await PluginPortalDataCache.GetReleasesAsync() : []);

            var notices = new List<PluginUpdateNotice>();
            foreach (var installed in installedUpdaters)
            {
                if (installed.CurrentVersion is null)
                    continue;

                var latestVersion = TryParseGitHubRepository(installed.PluginUrl, out var owner, out var repo)
                    ? GetLatestVersion(latestReleases, ToRepositoryKey(owner, repo))
                    : await PluginPortalDataCache.GetLatestVersionAsync(installed.Updater);

                if (latestVersion is not null && latestVersion.CompareTo(installed.CurrentVersion) > 0)
                    notices.Add(new PluginUpdateNotice(installed.Name, installed.PluginUrl, installed.CurrentVersion, latestVersion));
            }
            return notices;
        }

        private static Dictionary<(string Owner, string Repo), GitHubReleasesPluginSummary> GetLatestReleases(IEnumerable<GitHubReleasesPluginSummary> releases) =>
            releases
                .Where(r => !r.Prerelease)
                .GroupBy(r => ToRepositoryKey(r.User, r.Repo))
                .ToDictionary(g => g.Key, g => g.MaxBy(r => r.PublishedAt)!);

        private static PluginVersion? GetLatestVersion(
            Dictionary<(string Owner, string Repo), GitHubReleasesPluginSummary> latestReleases,
            (string Owner, string Repo) repository) =>
            latestReleases.TryGetValue(repository, out var release) ? ParseReleaseVersion(release) : null;

        private static (string Owner, string Repo) ToRepositoryKey(string owner, string repo) =>
            (owner.ToLowerInvariant(), repo.ToLowerInvariant());

        public static async Task<PluginVersion?> GetLatestVersionAsync(IPluginUpdater updater)
        {
            try
            {
                var updates = await updater.GetUpdatesAsync();
                return updates
                    .Where(u => !u.IsPrerelease)
                    .Select(u => u.Version)
                    .Max();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check plugin update ({updater.PluginUrl}): {ex.Message}");
                return null;
            }
        }

        private static List<InstalledUpdater> GetInstalledUpdaters()
        {
            var result = new List<InstalledUpdater>();
            foreach (var plugin in PluginLoader.UserPlugins)
            {
                try
                {
#pragma warning disable CS0618
                    var updater = plugin.Updater;
#pragma warning restore CS0618

                    if (updater is null || string.IsNullOrWhiteSpace(updater.PluginUrl))
                        continue;

                    if (result.Any(x => x.PluginUrl == updater.PluginUrl))
                        continue;

                    result.Add(new InstalledUpdater(plugin.Name, updater, updater.PluginUrl, GetCurrentVersion(updater)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to get plugin updater ({plugin.Name}): {ex.Message}");
                }
            }
            return result;
        }

        private static PluginVersion? GetCurrentVersion(IPluginUpdater updater)
        {
            try
            {
                return updater.CurrentVersion;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get plugin version ({updater.PluginUrl}): {ex.Message}");
                return null;
            }
        }

        public static string? FindGitHubRepositoryUrl(PluginCatalogItem? plugin)
        {
            if (plugin is null) return null;

            if (plugin.Url is not null && plugin.Url.Contains("github.com"))
            {
                return plugin.Url;
            }

            if (plugin.Links is not null)
            {
                return plugin.Links.FirstOrDefault(link => link?.Contains("github.com") ?? false);
            }

            return null;
        }

        public static bool TryParseGitHubRepository(
            string? url,
            [NotNullWhen(true)] out string? owner,
            [NotNullWhen(true)] out string? repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrEmpty(url))
                return false;

            var match = GitHubRepositoryUrlRegex().Match(url);
            if (!match.Success)
                return false;

            owner = match.Groups[1].Value;
            repo = match.Groups[2].Value;
            return true;
        }

        private static PluginVersion? ParseReleaseVersion(GitHubReleasesPluginSummary release)
        {
            foreach (var text in new[] { release.Name, release.TagName })
            {
                if (string.IsNullOrEmpty(text))
                    continue;
                var match = VersionRegex().Match(text);
                if (match.Success && PluginVersion.TryParse(match.Value, out var version))
                    return version;
            }
            return null;
        }

        public static string FormatVersion(PluginVersion version) =>
            string.IsNullOrEmpty(version.Suffix) ? version.Version.ToString() : $"{version.Version}-{version.Suffix}";

        private static string NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            var text = url.Trim().ToLowerInvariant();
            foreach (var scheme in new[] { "https://", "http://" })
            {
                if (text.StartsWith(scheme, StringComparison.Ordinal))
                {
                    text = text[scheme.Length..];
                    break;
                }
            }
            if (text.StartsWith("www.", StringComparison.Ordinal))
                text = text[4..];
            return text.TrimEnd('/');
        }

        [GeneratedRegex(@"github\.com/([^/]+)/([^/]+)")]
        private static partial Regex GitHubRepositoryUrlRegex();

        [GeneratedRegex(@"\d+(\.\d+){1,3}(-[^\s]+)?")]
        private static partial Regex VersionRegex();
    }
}
