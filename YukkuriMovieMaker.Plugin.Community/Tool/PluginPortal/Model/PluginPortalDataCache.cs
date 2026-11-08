using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal static class PluginPortalDataCache
    {
        private static readonly Lock gate = new();
        private static readonly Dictionary<string, Task<PluginVersion?>> latestVersionTasks = [];
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

        public static Task<PluginVersion?> GetLatestVersionAsync(IPluginUpdater updater)
        {
            lock (gate)
            {
                if (!latestVersionTasks.TryGetValue(updater.PluginUrl, out var task) || task is { IsCompleted: true, Result: null })
                {
                    task = Task.Run(() => PluginUpdateChecker.GetLatestVersionAsync(updater));
                    latestVersionTasks[updater.PluginUrl] = task;
                }
                return task;
            }
        }
    }
}
