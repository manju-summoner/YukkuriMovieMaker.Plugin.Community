using System.Diagnostics;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal static class PluginPortalDataCache
    {
        private static readonly Lock gate = new();
        private static readonly Dictionary<string, Task<PluginUpdateChecker.LatestUpdate?>> latestUpdateTasks = [];
        private static Task<PluginCatalog>? catalogTask;
        private static Task<GitHubReleasesPluginSummary[]>? releasesTask;

        public static Task<PluginCatalog> GetCatalogAsync()
        {
            lock (gate)
            {
                if (catalogTask is null || catalogTask.IsFaulted || catalogTask.IsCanceled)
                    catalogTask = PluginCatalog.GetPluginCatalogAsync();
                return catalogTask;
            }
        }

        public static Task<GitHubReleasesPluginSummary[]> GetReleasesAsync()
        {
            lock (gate)
            {
                if (releasesTask is null || releasesTask.IsFaulted || releasesTask.IsCanceled)
                    releasesTask = GitHubReleasesPluginSummaries.GetSummariesAsync(false);
                return releasesTask;
            }
        }

        public static async Task<PluginVersion?> GetLatestVersionAsync(IPluginUpdater updater) =>
            (await GetLatestUpdateAsync(updater))?.Version;

        public static async Task<PluginUpdateChecker.LatestUpdate?> GetLatestUpdateAsync(IPluginUpdater updater)
        {
            try
            {
                return await GetOrStartLatestUpdateTask(updater);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check plugin update ({updater.PluginUrl}): {ex.Message}");
                return null;
            }
        }

        private static Task<PluginUpdateChecker.LatestUpdate?> GetOrStartLatestUpdateTask(IPluginUpdater updater)
        {
            lock (gate)
            {
                if (!latestUpdateTasks.TryGetValue(updater.PluginUrl, out var task) || task.IsFaulted || task.IsCanceled)
                {
                    task = Task.Run(() => PluginUpdateChecker.GetLatestUpdateAsync(updater));
                    latestUpdateTasks[updater.PluginUrl] = task;
                }
                return task;
            }
        }
    }
}
