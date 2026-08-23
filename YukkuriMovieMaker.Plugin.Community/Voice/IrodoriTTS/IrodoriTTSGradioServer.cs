using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal static class IrodoriTTSGradioServer
{
    static Process? managedProcess;
    static JobObject? jobObject;
    static string? currentApp;

    const int HealthCheckIntervalMs = 500;
    const int HealthCheckTimeoutMs = 120_000;
    // 依存関係のダウンロードは数GBに達するため、同期を伴う起動は長く待つ。
    // ヘルスチェックは失敗のたびにログへ残るので、待つ間は間隔も広げる。
    const int SyncHealthCheckIntervalMs = 5_000;
    const int SyncHealthCheckTimeoutMs = 1_800_000;
    const string DefaultTTSUrl = "http://127.0.0.1:7860";
    const string DefaultVoiceDesignUrl = "http://127.0.0.1:7861";
    const string CudaExtraPrefix = "cu";
    const string CpuExtraName = "cpu";
    const string DefaultUvEnvironmentName = ".venv";

    static readonly Regex extraDefinitionRegex = new(@"^""?([A-Za-z0-9._-]+)""?\s*=\s*\[");
    static readonly Regex torchVersionRegex = new(@"__version__\s*=\s*['""]([^'""]+)['""]");

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

        var workingDirectory = Path.GetDirectoryName(gradioAppFullPath) ?? string.Empty;
        var scriptFileName = Path.GetFileName(gradioAppFullPath);
        var showConsole = settings.ShowConsoleWindow;

        // まず導入済みの依存関係をそのまま使う
        var result = await StartServerAsync(
            BuildUvArguments(scriptFileName, settings.ServerPort, extra: null, noSync: true),
            workingDirectory, gradioAppFullPath, localUrl, HealthCheckTimeoutMs, HealthCheckIntervalMs, showConsole);
        if (result == StartResult.Started)
            return localUrl;

        if (!ShouldRetryWithSync(result))
            throw result == StartResult.TimedOut
                ? new TimeoutException(Texts.FailedToConnect)
                : new InvalidOperationException(Texts.FailedToConnect);

        // 依存関係が足りず起動できなかったので、環境に合う extra を選んで導入する
        Log.Default.Write("Irodori-TTS failed to start without syncing. retrying with sync.");
        var extra = await Task.Run(() => ResolveUvExtra(workingDirectory));
        result = await StartServerAsync(
            BuildUvArguments(scriptFileName, settings.ServerPort, extra, noSync: false),
            workingDirectory, gradioAppFullPath, localUrl, SyncHealthCheckTimeoutMs, SyncHealthCheckIntervalMs, showConsole);
        if (result == StartResult.Started)
            return localUrl;

        throw result == StartResult.TimedOut
            ? new TimeoutException(Texts.FailedToConnect)
            : new InvalidOperationException(Texts.FailedToConnect);
    }

    internal enum StartResult
    {
        Started,
        Failed,
        TimedOut,
    }

    /// <summary>
    /// 依存関係を同期し直して起動をやり直すべきかどうか
    /// </summary>
    /// <remarks>
    /// 同期は利用者が選んだPyTorchのビルドを置き換えてしまうため、
    /// 依存関係が足りずプロセスが即座に終了した場合に限る。
    /// タイムアウトは初回のモデル読み込みなど正常な起動でも起きるので対象外。
    /// </remarks>
    internal static bool ShouldRetryWithSync(StartResult result) => result == StartResult.Failed;

    /// <summary>
    /// uv でサーバーを起動し、応答を返すまで待つ
    /// </summary>
    static async Task<StartResult> StartServerAsync(string args, string workingDirectory, string gradioAppFullPath, string localUrl, int timeoutMs, int intervalMs, bool showConsole)
    {
        managedProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "uv",
            Arguments = args,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = !showConsole,
            UseShellExecute = showConsole,
        });
        if (managedProcess is null)
            return StartResult.Failed;

        // JobObject に登録: YMM4終了時に自動Kill
        jobObject?.Dispose();
        jobObject = JobObject.CreateAsKillOnJobClose();
        jobObject.AssignProcess(managedProcess);

        currentApp = gradioAppFullPath;

        // ヘルスチェック待機（ローカルURLで確認）
        var elapsed = 0;
        while (elapsed < timeoutMs)
        {
            if (managedProcess.HasExited)
            {
                KillManagedProcess();
                return StartResult.Failed;
            }

            if (await IrodoriTTSAPI.HealthCheckAsync(localUrl))
                return StartResult.Started;

            await Task.Delay(intervalMs);
            elapsed += intervalMs;
        }

        KillManagedProcess();
        return StartResult.TimedOut;
    }

    /// <summary>
    /// uv の起動引数を組み立てる
    /// </summary>
    /// <remarks>
    /// --no-sync が無いと uv run が lock ファイルに沿って環境を再同期し、
    /// 利用者が導入したCUDA版PyTorchがCPU版へ置き換わってGPUが使えなくなる。
    /// 未同期の環境では依存関係が入らないので、そのときだけ extra を選んで同期させる。
    /// </remarks>
    internal static string BuildUvArguments(string scriptFileName, int port, string? extra, bool noSync)
    {
        var syncOption =
            noSync ? "--no-sync " :
            !string.IsNullOrEmpty(extra) ? $"--extra {extra} " :
            string.Empty;
        return $"run {syncOption}python \"{scriptFileName}\" --server-name 127.0.0.1 --server-port {port}";
    }

    /// <summary>
    /// 依存関係を導入するときに指定する extra を選ぶ
    /// </summary>
    /// <remarks>
    /// Irodori-TTS は PyTorch のビルドを extra で切り替えるため、
    /// 指定しないとCPU版が入ってGPUが使われない。
    /// extra 名は上流の更新で変わるので pyproject.toml に実在するものから選ぶ。
    /// </remarks>
    internal static string? ResolveUvExtra(string workingDirectory)
        => ResolveUvExtra(workingDirectory, HasCudaDevice());

    internal static string? ResolveUvExtra(string workingDirectory, bool hasCudaDevice)
    {
        if (string.IsNullOrEmpty(workingDirectory))
            return null;

        var pyprojectPath = Path.Combine(workingDirectory, "pyproject.toml");
        if (!File.Exists(pyprojectPath))
            return null;

        try
        {
            var availableExtras = ParseUvExtras(File.ReadAllText(pyprojectPath));

            // 依存関係の不足以外の理由で起動に失敗したときも再同期は走るため、
            // 導入済みの構成があるならそれを指定し直してビルドの入れ替わりを防ぐ
            return SelectExtraForInstalledTorch(availableExtras, ReadInstalledTorchVersion(workingDirectory))
                ?? SelectUvExtra(availableExtras, hasCudaDevice);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Default.Write("Irodori-TTS failed to read pyproject.toml", ex);
            return null;
        }
    }

    /// <summary>
    /// 導入済みの PyTorch のビルドに対応する extra を返す（判別できなければ null）
    /// </summary>
    /// <remarks>
    /// PyTorch のバージョンは 2.10.0+cu128 のようにビルドがローカルバージョンとして付く。
    /// これを extra 名に対応付けることで、rocm や xpu を選んでいる環境も維持できる。
    /// </remarks>
    internal static string? SelectExtraForInstalledTorch(IReadOnlyList<string> availableExtras, string? installedTorchVersion)
    {
        if (string.IsNullOrEmpty(installedTorchVersion))
            return null;

        var separator = installedTorchVersion.IndexOf('+');
        if (separator < 0)
            return null;

        var build = installedTorchVersion[(separator + 1)..];
        return availableExtras
            .Where(extra => build.StartsWith(extra, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(extra => extra.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// uv が使う仮想環境のディレクトリを求める
    /// </summary>
    /// <remarks>
    /// UV_PROJECT_ENVIRONMENT を設定していると uv は既定の .venv 以外を使う。
    /// ここで取り違えると導入済みの構成を読み損ね、再同期でビルドが入れ替わってしまう。
    /// </remarks>
    internal static string ResolveUvEnvironmentDirectory(string workingDirectory, string? projectEnvironment)
    {
        if (string.IsNullOrWhiteSpace(projectEnvironment))
            return Path.Combine(workingDirectory, DefaultUvEnvironmentName);

        // 相対パスはプロジェクトディレクトリからの相対として解決される
        return Path.IsPathRooted(projectEnvironment)
            ? projectEnvironment
            : Path.Combine(workingDirectory, projectEnvironment);
    }

    static string? ReadInstalledTorchVersion(string workingDirectory)
    {
        var environmentDirectory = ResolveUvEnvironmentDirectory(
            workingDirectory,
            Environment.GetEnvironmentVariable("UV_PROJECT_ENVIRONMENT"));
        var versionPath = Path.Combine(environmentDirectory, "Lib", "site-packages", "torch", "version.py");
        if (!File.Exists(versionPath))
            return null;

        try
        {
            var match = torchVersionRegex.Match(File.ReadAllText(versionPath));
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Default.Write("Irodori-TTS failed to read the installed PyTorch version", ex);
            return null;
        }
    }

    static bool HasCudaDevice()
    {
        if (CudaDriver.TryInitialize(out var failureReason))
            return true;

        // GPUがあるのにCPU版が選ばれたときの切り分けに使う
        Log.Default.Write($"Irodori-TTS could not use CUDA: {failureReason}");
        return false;
    }

    /// <summary>
    /// pyproject.toml の [project.optional-dependencies] に定義された extra 名を取り出す
    /// </summary>
    internal static IReadOnlyList<string> ParseUvExtras(string pyprojectToml)
    {
        var extras = new List<string>();
        var isInOptionalDependencies = false;
        foreach (var rawLine in pyprojectToml.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                isInOptionalDependencies = line.Replace(" ", "").StartsWith("[project.optional-dependencies]", StringComparison.Ordinal);
                continue;
            }
            if (!isInOptionalDependencies)
                continue;

            // 配列の中身（"torch>=2.0" など）を拾わないよう、値が [ で始まる行だけを見る
            var match = extraDefinitionRegex.Match(line);
            if (match.Success)
                extras.Add(match.Groups[1].Value);
        }
        return extras;
    }

    /// <summary>
    /// 実在する extra の中から環境に合うものを選ぶ（該当が無ければ null）
    /// </summary>
    /// <remarks>
    /// 新規に導入するときは CUDA 版か CPU 版しか選ばない。
    /// Windows では PyTorch の rocm・xpu ビルドの対応が限られるため、
    /// これらは利用者が自分で導入した場合にだけ維持する。
    /// </remarks>
    internal static string? SelectUvExtra(IReadOnlyList<string> availableExtras, bool hasCudaDevice)
    {
        if (hasCudaDevice)
        {
            var cudaExtra = availableExtras
                .Select(extra => (Name: extra, Version: ParseCudaExtraVersion(extra)))
                .Where(x => x.Version > 0)
                .OrderByDescending(x => x.Version)
                .Select(x => x.Name)
                .FirstOrDefault();
            if (cudaExtra is not null)
                return cudaExtra;
        }

        return availableExtras.FirstOrDefault(extra => extra == CpuExtraName);
    }

    /// <remarks>
    /// cu128・cu130 のように桁数が揃っている前提で数値比較する。
    /// 上流が cu13 のような桁数の違う名前を使い始めたら比較方法を見直すこと。
    /// </remarks>
    static int ParseCudaExtraVersion(string extra)
    {
        if (!extra.StartsWith(CudaExtraPrefix, StringComparison.Ordinal))
            return 0;

        var version = extra[CudaExtraPrefix.Length..];
        return version.Length > 0 && version.All(char.IsAsciiDigit) && int.TryParse(version, out var parsed) ? parsed : 0;
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

        jobObject?.Dispose();
        jobObject = null;

        currentApp = null;
    }
}
