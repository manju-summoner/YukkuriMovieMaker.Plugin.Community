using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal static class IrodoriTTSGradioServer
{
    static Process? managedProcess;
    static JobObject? jobObject;
    static string? currentApp;

    const int HealthCheckIntervalMs = 500;
    const int HealthCheckTimeoutMs = 120_000;
    const string DefaultTTSUrl = "http://127.0.0.1:7860";
    const string DefaultVoiceDesignUrl = "http://127.0.0.1:7861";

    static string ResolveTTSUrl()
    {
        var url = IrodoriTTSSettings.Default.TTSUrl;
        return string.IsNullOrWhiteSpace(url) ? DefaultTTSUrl : url;
    }

    static string ResolveVoiceDesignUrl()
    {
        var url = IrodoriTTSSettings.Default.VoiceDesignUrl;
        return string.IsNullOrWhiteSpace(url) ? DefaultVoiceDesignUrl : url;
    }

    public static Task<string> EnsureTTSServerAsync()
    {
        var gradioAppPath = IrodoriTTSSettings.Default.GradioAppPath;
        return EnsureServerAsync(
            ResolveTTSUrl(),
            gradioAppPath);
    }

    public static Task<string> EnsureVoiceDesignServerAsync()
    {
        var gradioAppPath = IrodoriTTSSettings.Default.GradioAppPath;
        // gradio_app.py → gradio_app_voicedesign.py に置換
        var dir = Path.GetDirectoryName(gradioAppPath) ?? string.Empty;
        var voiceDesignPath = Path.Combine(dir, "gradio_app_voicedesign.py");
        return EnsureServerAsync(
            ResolveVoiceDesignUrl(),
            voiceDesignPath);
    }

    public static void Shutdown()
    {
        KillManagedProcess();
    }

    /// <summary>
    /// YMM4管理プロセスが実行中かどうか
    /// </summary>
    public static bool IsRunning => managedProcess != null && !managedProcess.HasExited;

    /// <summary>
    /// 現在起動中のアプリのファイル名（実行中でない場合は null）
    /// </summary>
    public static string? CurrentAppName => IsRunning && currentApp != null ? Path.GetFileName(currentApp) : null;

    static string LocalServerUrl =>
        $"http://127.0.0.1:{IrodoriTTSSettings.Default.ServerPort}";

    static async Task<string> EnsureServerAsync(string url, string gradioAppFullPath)
    {
        // YMM4管理プロセスが別アプリで起動中なら、先に切り替える
        // （同じポートで別アプリが動いていると、ヘルスチェックが誤って通るため）
        if (managedProcess != null && !managedProcess.HasExited && currentApp != gradioAppFullPath)
        {
            KillManagedProcess();
            // 切り替え後、ポートが解放されるまで少し待つ
            await Task.Delay(1000);
        }

        // ユーザー管理モード: 指定URLに既にサーバーが起動していればそのまま使う
        if (await IrodoriTTSAPI.HealthCheckAsync(url))
            return url;

        // YMM4管理モード: サーバーを起動する
        var settings = IrodoriTTSSettings.Default;
        if (string.IsNullOrEmpty(settings.GradioAppPath))
            throw new InvalidOperationException(Texts.ServerNotConfigured);

        // ローカルサーバーURL（localhost + ServerPort）
        var localUrl = LocalServerUrl;

        // 既に同じアプリが起動中なら、ローカルURLでヘルスチェック
        if (managedProcess != null && !managedProcess.HasExited)
        {
            if (await IrodoriTTSAPI.HealthCheckAsync(localUrl))
                return localUrl;
        }

        // 新しいプロセスを起動（uv run python {gradio_app.py} --server-name 127.0.0.1 --server-port {port}）
        var workingDirectory = Path.GetDirectoryName(gradioAppFullPath) ?? string.Empty;
        var scriptFileName = Path.GetFileName(gradioAppFullPath);
        var args = $"run python {scriptFileName} --server-name 127.0.0.1 --server-port {settings.ServerPort}";

        var showConsole = settings.ShowConsoleWindow;
        managedProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "uv",
            Arguments = args,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = !showConsole,
            UseShellExecute = showConsole,
        }) ?? throw new InvalidOperationException(Texts.FailedToConnect);

        // JobObject に登録: YMM4終了時に自動Kill
        jobObject?.Dispose();
        jobObject = JobObject.CreateAsKillOnJobClose();
        jobObject.AssignProcess(managedProcess);

        currentApp = gradioAppFullPath;

        // ヘルスチェック待機（ローカルURLで確認）
        var elapsed = 0;
        while (elapsed < HealthCheckTimeoutMs)
        {
            if (managedProcess.HasExited)
                throw new InvalidOperationException(Texts.FailedToConnect);

            if (await IrodoriTTSAPI.HealthCheckAsync(localUrl))
                return localUrl;

            await Task.Delay(HealthCheckIntervalMs);
            elapsed += HealthCheckIntervalMs;
        }

        KillManagedProcess();
        throw new TimeoutException(Texts.FailedToConnect);
    }

    static void KillManagedProcess()
    {
        if (managedProcess != null)
        {
            try
            {
                if (!managedProcess.HasExited)
                    managedProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex) { Log.Default.Write("Irodori-TTS failed to kill server process", ex); }
            managedProcess.Dispose();
            managedProcess = null;
        }

        if (jobObject != null)
        {
            jobObject.Dispose();
            jobObject = null;
        }

        currentApp = null;
    }
}
