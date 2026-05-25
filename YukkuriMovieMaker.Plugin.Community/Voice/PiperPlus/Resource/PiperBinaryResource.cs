using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PiperBinaryResource
{
    const string ExecutableName = "PiperPlus.Cli.exe";
    const string RepoOwner = "ayutaz";
    const string RepoName = "piper-plus";

    static readonly EnumerationOptions SearchOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
    };

    static readonly SemaphoreSlim InstallGate = new(1, 1);
    static readonly object CacheLock = new();
    static string? resolvedExecutablePath;

    static string InstalledVersionFilePath =>
        Path.Combine(PiperPlusSettings.Default.BinaryDirectory, "installed_version.txt");

    public static string? InstalledVersion
    {
        get
        {
            var path = InstalledVersionFilePath;
            if (!File.Exists(path))
                return null;
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    static string InstallDirectory(string version) =>
        Path.Combine(PiperPlusSettings.Default.BinaryDirectory, version);

    public static string? ExecutablePath
    {
        get
        {
            lock (CacheLock)
            {
                if (resolvedExecutablePath is not null && File.Exists(resolvedExecutablePath))
                    return resolvedExecutablePath;

                var version = InstalledVersion;
                if (version is null)
                {
                    resolvedExecutablePath = null;
                    return null;
                }

                var dir = InstallDirectory(version);
                resolvedExecutablePath = Directory.Exists(dir)
                    ? Directory.EnumerateFiles(dir, ExecutableName, SearchOptions).FirstOrDefault()
                    : null;

                return resolvedExecutablePath;
            }
        }
    }

    public static bool IsReady => ExecutablePath is not null;

    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            resolvedExecutablePath = null;
        }
    }

    public static async Task EnsureAsync(
        string version,
        IProgress<(double Progress, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Piper Plus requires Windows.");

        if (!TryResolveAssetName(out var assetName))
            throw new PlatformNotSupportedException(
                $"Piper Plus does not support this architecture. Process: {RuntimeInformation.ProcessArchitecture}, OS: {RuntimeInformation.OSArchitecture}.");

        await InstallGate.WaitAsync(cancellationToken);
        try
        {
            var currentVersion = InstalledVersion;
            if (currentVersion == version && IsReady)
                return;

            await InstallCoreAsync(version, assetName, currentVersion, progress, cancellationToken);
        }
        finally
        {
            InstallGate.Release();
        }
    }

    static bool TryResolveAssetName(out string assetName)
    {
        assetName = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "piper-plus-cli-win-x64.zip",
            Architecture.Arm64 => "piper-plus-cli-win-arm64.zip",
            _ => string.Empty,
        };
        return assetName.Length > 0;
    }

    static async Task InstallCoreAsync(
        string version,
        string assetName,
        string? currentVersion,
        IProgress<(double Progress, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        var targetDir = InstallDirectory(version);
        var tempDir = targetDir + ".tmp";
        var currentDir = currentVersion is not null ? InstallDirectory(currentVersion) : null;
        var backupDir = currentDir is not null ? currentDir + ".bak" : null;

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);

        Directory.CreateDirectory(tempDir);

        await DownloadAndExtractAsync(version, assetName, tempDir, progress, cancellationToken);

        CommitInstall(version, currentDir, backupDir, targetDir, tempDir);

        progress?.Report((1.0, Texts.BinaryReady));
    }

    static async Task DownloadAndExtractAsync(
        string version,
        string assetName,
        string tempDir,
        IProgress<(double Progress, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(tempDir, assetName);
        var downloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{version}/{assetName}";

        var client = HttpClientFactory.Client;
        using var response = await client.GetAsync(
            downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = File.Create(zipPath))
        {
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;
                if (totalBytes > 0)
                    progress?.Report(((double)downloaded / totalBytes * 0.9, Texts.DownloadingBinary));
            }
        }

        progress?.Report((0.9, Texts.ExtractingBinary));
        await Task.Run(
            () => ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true),
            cancellationToken);

        try { File.Delete(zipPath); } catch { }
    }

    static void CommitInstall(
        string version,
        string? currentDir,
        string? backupDir,
        string targetDir,
        string tempDir)
    {
        var backedUp = false;

        if (currentDir is not null && backupDir is not null && Directory.Exists(currentDir))
        {
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, recursive: true);
            Directory.Move(currentDir, backupDir);
            backedUp = true;
        }

        try
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);

            Directory.Move(tempDir, targetDir);

            var newExecutable = Directory.Exists(targetDir)
                ? Directory.EnumerateFiles(targetDir, ExecutableName, SearchOptions).FirstOrDefault()
                : null;

            if (newExecutable is null)
                throw new FileNotFoundException(
                    $"Piper Plus CLI executable not found after extraction in '{targetDir}'.");

            File.WriteAllText(InstalledVersionFilePath, version);

            if (backedUp && backupDir is not null && Directory.Exists(backupDir))
                try { Directory.Delete(backupDir, recursive: true); } catch { }

            lock (CacheLock)
            {
                resolvedExecutablePath = newExecutable;
            }
        }
        catch
        {
            try
            {
                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, recursive: true);
            }
            catch { }

            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { }

            if (backedUp && backupDir is not null && currentDir is not null && Directory.Exists(backupDir))
                try { Directory.Move(backupDir, currentDir); } catch { }

            throw;
        }
    }
}
