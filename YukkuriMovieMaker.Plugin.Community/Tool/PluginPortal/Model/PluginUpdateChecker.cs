using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal static partial class PluginUpdateChecker
    {
        internal sealed record InstalledUpdater(string Name, IPluginUpdater Updater, string PluginUrl, PluginVersion? CurrentVersion);

        internal sealed record LatestUpdate(PluginVersion Version, IPluginUpdateInfo? DownloadableUpdate);

        public static Dictionary<PluginCatalogItem, PluginUpdateStatus> CheckWithReleaseList(
            IEnumerable<PluginCatalogItem> catalog,
            IEnumerable<GitHubReleasesPluginSummary> releases,
            out List<(PluginCatalogItem Plugin, IPluginUpdater Updater)> otherSiteUpdaters) =>
            CheckWithReleaseList(catalog, releases, GetInstalledUpdaters(), out otherSiteUpdaters);

        internal static Dictionary<PluginCatalogItem, PluginUpdateStatus> CheckWithReleaseList(
            IEnumerable<PluginCatalogItem> catalog,
            IEnumerable<GitHubReleasesPluginSummary> releases,
            IReadOnlyList<InstalledUpdater> installedUpdaters,
            out List<(PluginCatalogItem Plugin, IPluginUpdater Updater)> otherSiteUpdaters)
        {
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

                (string Owner, string Repo)? repository =
                    TryParseGitHubRepository(FindGitHubRepositoryUrl(plugin), out var owner, out var repo)
                        ? ToRepositoryKey(owner, repo)
                        : null;

                var installed = installedUpdaters.FirstOrDefault(u =>
                    pluginUrls.Contains(NormalizeUrl(u.PluginUrl))
                    || (repository is not null
                        && TryParseGitHubRepository(u.PluginUrl, out var updaterOwner, out var updaterRepo)
                        && ToRepositoryKey(updaterOwner, updaterRepo) == repository));

                //カタログにGitHubのリンクがあっても、Updaterが外部サイトを指していればそちらで配布されている
                if (installed is not null && !TryParseGitHubRepository(installed.PluginUrl, out _, out _))
                {
                    statuses[plugin] = new PluginUpdateStatus(
                        installed.CurrentVersion,
                        null,
                        installed.PluginUrl,
                        HasGitHubRelease: repository is { } releasedRepository && latestReleases.ContainsKey(releasedRepository));
                    otherSiteUpdaters.Add((plugin, installed.Updater));
                }
                else if (repository is { } gitHubRepository)
                {
                    statuses[plugin] = new PluginUpdateStatus(installed?.CurrentVersion, GetLatestVersion(latestReleases, gitHubRepository), null);
                }
            }

            return statuses;
        }

        public static Task<IReadOnlyList<PluginUpdateNotice>> CheckInstalledPluginsAsync() =>
            CheckInstalledPluginsAsync(GetInstalledUpdaters(), PluginPortalDataCache.GetReleasesAsync);

        internal static async Task<IReadOnlyList<PluginUpdateNotice>> CheckInstalledPluginsAsync(
            IReadOnlyList<InstalledUpdater> installedUpdaters,
            Func<Task<GitHubReleasesPluginSummary[]>> getReleasesAsync)
        {
            if (installedUpdaters.Count == 0)
                return [];

            var hasGitHubUpdater = installedUpdaters.Any(u => TryParseGitHubRepository(u.PluginUrl, out _, out _));
            var latestReleases = GetLatestReleases(hasGitHubUpdater ? await GetReleasesOrEmptyAsync(getReleasesAsync) : []);

            var targets = installedUpdaters.Where(u => u.CurrentVersion is not null).ToArray();
            var latestVersions = await Task.WhenAll(targets.Select(installed =>
                TryParseGitHubRepository(installed.PluginUrl, out var owner, out var repo)
                    ? Task.FromResult(GetLatestVersion(latestReleases, ToRepositoryKey(owner, repo)))
                    : PluginPortalDataCache.GetLatestVersionAsync(installed.Updater)));

            var notices = new List<PluginUpdateNotice>();
            foreach (var (installed, latestVersion) in targets.Zip(latestVersions))
            {
                if (installed.CurrentVersion is { } currentVersion && latestVersion is not null && latestVersion.CompareTo(currentVersion) > 0)
                    notices.Add(new PluginUpdateNotice(installed.Name, installed.PluginUrl, currentVersion, latestVersion));
            }
            return notices;
        }

        //GitHubの一覧を取れなくても、外部サイトで配布されているプラグインの確認は続ける
        private static async Task<GitHubReleasesPluginSummary[]> GetReleasesOrEmptyAsync(Func<Task<GitHubReleasesPluginSummary[]>> getReleasesAsync)
        {
            try
            {
                return await getReleasesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get GitHub releases: {ex.Message}");
                return [];
            }
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

        //プラグインのUpdaterのメンバーは例外を投げうるため、ダウンロードできるかもここで読んでおく
        public static async Task<LatestUpdate?> GetLatestUpdateAsync(IPluginUpdater updater)
        {
            var updates = await updater.GetUpdatesAsync();
            var latest = updates
                .Where(u => !u.IsPrerelease)
                .MaxBy(u => u.Version);
            return latest is null ? null : new LatestUpdate(latest.Version, CanDownload(latest) ? latest : null);
        }

        //CanDownloadはこのポータルで初めて読まれるため、未実装で例外を投げるプラグインがあっても最新のバージョンの確認は続ける
        private static bool CanDownload(IPluginUpdateInfo update)
        {
            try
            {
                return update.CanDownload;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check whether the plugin update can be downloaded ({update.Version}): {ex.Message}");
                return false;
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

            return new[] { plugin.Url }
                .Concat(plugin.Links ?? [])
                .FirstOrDefault(url => TryParseGitHubRepository(url, out _, out _));
        }

        public static PluginUpdateStatus WithLatestUpdate(PluginUpdateStatus status, LatestUpdate? latestUpdate) =>
            status with
            {
                LatestVersion = latestUpdate?.Version,
                DownloadableUpdate = latestUpdate?.DownloadableUpdate,
                IsLatestUpdateChecked = true,
            };

        //インストール済みの外部サイトのUpdaterでダウンロードできればそれを使い、できなければGitHubのリリースで公開されているときだけGitHubから取る。
        //Updaterの確認が終わる前にGitHubから取らせないよう、確認が終わるまではGitHubのボタンを出さない
        public static bool CanDownloadFromGitHub(PluginCatalogItem? plugin, PluginUpdateStatus? status) =>
            status?.DownloadableUpdate is null
            && FindGitHubRepositoryUrl(plugin) is not null
            && (status?.PageUrl is null || (status.IsLatestUpdateChecked && status.HasGitHubRelease));

        public static bool TryParseGitHubRepository(
            string? url,
            [NotNullWhen(true)] out string? owner,
            [NotNullWhen(true)] out string? repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
                return false;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                return false;
            if (uri.Host is not ("github.com" or "www.github.com"))
                return false;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                return false;

            var repositoryName = segments[1];
            if (repositoryName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                repositoryName = repositoryName[..^4];
            if (repositoryName.Length == 0)
                return false;

            owner = segments[0];
            repo = repositoryName;
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

            //BOOTHの商品ページは https://<ショップ>.booth.pm/items/<ID> と https://booth.pm/ja/items/<ID> のどちらでも書けるため、商品のIDで突き合わせる
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && (uri.Host == "booth.pm" || uri.Host.EndsWith(".booth.pm", StringComparison.Ordinal))
                && BoothItemPathRegex().Match(uri.AbsolutePath) is { Success: true } boothItem)
                return $"booth.pm/items/{boothItem.Groups[1].Value}";

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

        [GeneratedRegex(@"\d+(\.\d+){1,3}(-[^\s]+)?")]
        private static partial Regex VersionRegex();

        [GeneratedRegex(@"^/(?:[^/]+/)?items/(\d+)/?$")]
        private static partial Regex BoothItemPathRegex();
    }
}
