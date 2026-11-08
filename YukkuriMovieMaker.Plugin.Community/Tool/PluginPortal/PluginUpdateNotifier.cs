using System.Diagnostics;
using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal static class PluginUpdateNotifier
    {
        private static readonly TimeSpan mainWindowPollInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan mainWindowWaitLimit = TimeSpan.FromMinutes(5);

        public static void Start() => Task.Run(CheckAndNotifyAsync);

        private static async Task CheckAndNotifyAsync()
        {
            try
            {
                if (!PluginLoader.UserPlugins.Any() || !PluginPortalSettings.Default.IsCheckUpdatesOnStartup)
                    return;

                var notices = await PluginUpdateChecker.CheckInstalledPluginsAsync();
                var targets = GetNotificationTargets(notices);
                if (targets.Count == 0)
                    return;

                var application = Application.Current;
                if (application is null)
                    return;

                var limit = DateTime.UtcNow + mainWindowWaitLimit;
                while (DateTime.UtcNow < limit)
                {
                    if (await application.Dispatcher.InvokeAsync(() => TryShowNotification(notices, targets)))
                        return;
                    await Task.Delay(mainWindowPollInterval);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check plugin updates on startup: {ex.Message}");
            }
        }

        private static IReadOnlyList<PluginUpdateNotice> GetNotificationTargets(IReadOnlyList<PluginUpdateNotice> notices)
        {
            if (PluginPortalSettings.Default.IsNotifyEveryStartup)
                return notices;

            var alreadyNotified = PluginPortalSettings.Default.NotifiedVersions.ToHashSet();
            return [.. notices.Where(notice => !alreadyNotified.Contains(notice.NotificationKey))];
        }

        private static bool TryShowNotification(IReadOnlyList<PluginUpdateNotice> notices, IReadOnlyList<PluginUpdateNotice> targets)
        {
            if (Application.Current?.MainWindow is not { IsVisible: true, IsLoaded: true } mainWindow)
                return false;

            PluginPortalSettings.Default.NotifiedVersions = [.. notices.Select(notice => notice.NotificationKey)];
            PluginPortalSettings.Default.Save();

            if (new PluginUpdateNotificationWindow(targets, mainWindow).ShowDialog() == true)
                Settings.CommandRegistry.Execute(Settings.Commands.OpenToolWindow, new OpenToolWindowCommandArgs(typeof(PluginPortalViewModel)), mainWindow);

            return true;
        }
    }
}
