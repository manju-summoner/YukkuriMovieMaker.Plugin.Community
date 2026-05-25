using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PiperBinaryResource
{
    const string WindowsX64Asset = "piper-plus-cli-win-x64.zip";
    const string ExecutableName = "PiperPlus.Cli.exe";
    const string RepoOwner = "ayutaz";
    const string RepoName = "piper-plus";

    static readonly EnumerationOptions SearchOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
    };

    static readonly object CacheLock = new();
    static string? resolvedExecutablePath;

    static string InstalledVersionFilePath =>
        Path.Combine(PiperPlusSettings.Default.BinaryDirectory, "installed_version.txt");

    public static string? InstalledVersion
    {
        get
        {
            var path = InstalledVersionFilePath;
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.OSArchitecture != Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException(
                $"Piper Plus requires Windows x64. Detected OS architecture: {RuntimeInformation.OSArchitecture}, process architecture: {RuntimeInformation.ProcessArchitecture}.");

        var currentVersion = InstalledVersion;
        if (currentVersion == version && IsReady)
            return;

        var targetDir = InstallDirectory(version);
        var tempDir = targetDir + ".tmp";
        var currentDir = currentVersion is not null ? InstallDirectory(currentVersion) : null;
        var backupDir = currentDir is not null ? currentDir + ".bak" : null;

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);

        Directory.CreateDirectory(tempDir);

        var zipPath = Path.Combine(tempDir, WindowsX64Asset);
        var downloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{version}/{WindowsX64Asset}";

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

        if (currentDir is not null && backupDir is not null && Directory.Exists(currentDir))
        {
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, recursive: true);
            Directory.Move(currentDir, backupDir);
        }

        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);

        Directory.Move(tempDir, targetDir);

        InvalidateCache();

        var newExecutable = Directory.Exists(targetDir)
            ? Directory.EnumerateFiles(targetDir, ExecutableName, SearchOptions).FirstOrDefault()
            : null;

        if (newExecutable is null)
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);
            if (backupDir is not null && Directory.Exists(backupDir))
                Directory.Move(backupDir, currentDir!);
            throw new FileNotFoundException(
                $"Piper Plus CLI executable not found after extraction in '{targetDir}'.");
        }

        File.WriteAllText(InstalledVersionFilePath, version);

        if (backupDir is not null && Directory.Exists(backupDir))
        {
            try { Directory.Delete(backupDir, recursive: true); } catch { }
        }

        lock (CacheLock)
        {
            resolvedExecutablePath = newExecutable;
        }

        progress?.Report((1.0, Texts.BinaryReady));
    }
}
