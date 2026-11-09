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

                var application = Application.Current;
                if (application is null)
                    return;

                await CheckAndNotifyAsync(
                    () => WaitForMainWindowAsync(application),
                    PluginUpdateChecker.CheckInstalledPluginsAsync,
                    (notices, targets) => application.Dispatcher.InvokeAsync(() => ShowNotification(notices, targets)).Task);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check plugin updates on startup: {ex.Message}");
            }
        }

        //プラグインはCLIエンコードなどメイン画面を出さないプロセスでも読み込まれるため、メイン画面が出てから通信する
        internal static async Task CheckAndNotifyAsync(
            Func<Task<bool>> waitForMainWindowAsync,
            Func<Task<IReadOnlyList<PluginUpdateNotice>>> checkAsync,
            Func<IReadOnlyList<PluginUpdateNotice>, IReadOnlyList<PluginUpdateNotice>, Task> showAsync)
        {
            if (!await waitForMainWindowAsync())
                return;

            var notices = await checkAsync();
            var targets = GetNotificationTargets(notices);
            if (targets.Count == 0)
                return;

            await showAsync(notices, targets);
        }

        private static async Task<bool> WaitForMainWindowAsync(Application application)
        {
            var limit = DateTime.UtcNow + mainWindowWaitLimit;
            while (DateTime.UtcNow < limit)
            {
                if (await application.Dispatcher.InvokeAsync(() => FindMainWindow() is not null))
                    return true;
                await Task.Delay(mainWindowPollInterval);
            }
            return false;
        }

        //Application.MainWindowは最初に作られたウィンドウになるため、起動中の更新通知などのダイアログを指すことがある
        private static Window? FindMainWindow() =>
            ZOrderedWindowEnumerable.CreateMainWindows().FirstOrDefault(window => window is { IsVisible: true, IsLoaded: true });

        private static IReadOnlyList<PluginUpdateNotice> GetNotificationTargets(IReadOnlyList<PluginUpdateNotice> notices)
        {
            if (PluginPortalSettings.Default.IsNotifyEveryStartup)
                return notices;

            var alreadyNotified = PluginPortalSettings.Default.NotifiedVersions.ToHashSet();
            return [.. notices.Where(notice => !alreadyNotified.Contains(notice.NotificationKey))];
        }

        //配布元の一時的な失敗で今回の確認に出てこなかったプラグインの記録を消すと、次に取得できたときに再通知してしまうため残す
        internal static string[] MergeNotifiedVersions(IEnumerable<string> notifiedVersions, IReadOnlyList<PluginUpdateNotice> notices)
        {
            var checkedPrefixes = notices.Select(notice => notice.PluginUrl + "|").ToArray();
            return
            [
                .. notifiedVersions.Where(key => !checkedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))),
                .. notices.Select(notice => notice.NotificationKey),
            ];
        }

        private static void ShowNotification(IReadOnlyList<PluginUpdateNotice> notices, IReadOnlyList<PluginUpdateNotice> targets)
        {
            if (FindMainWindow() is not { } mainWindow)
                return;

            PluginPortalSettings.Default.NotifiedVersions = MergeNotifiedVersions(PluginPortalSettings.Default.NotifiedVersions, notices);
            PluginPortalSettings.Default.Save();

            if (new PluginUpdateNotificationWindow(targets, mainWindow).ShowDialog() == true)
                Settings.CommandRegistry.Execute(Settings.Commands.OpenToolWindow, new OpenToolWindowCommandArgs(typeof(PluginPortalViewModel)), mainWindow);
        }
    }
}
