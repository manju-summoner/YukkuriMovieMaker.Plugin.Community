using System.IO;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PiperBinaryResource
{
    const string ExecutableName = "PiperPlus.Cli.exe";
    const string RepoOwner = "ayutaz";
    const string RepoName = "piper-plus";
    const string InstalledVersionFileName = "installed_version.txt";

    static readonly SemaphoreSlim InstallGate = new(1, 1);

    static string InstalledVersionFilePath =>
        Path.Combine(PiperPlusPaths.BinaryDirectory, InstalledVersionFileName);

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
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public static string ExecutablePath => Path.Combine(PiperPlusPaths.BinaryDirectory, ExecutableName);

    public static bool IsReady => InstalledVersion is not null && File.Exists(ExecutablePath);

    public static async Task EnsureAsync(
        string version,
        ProgressMessage? progress = null,
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
            if (InstalledVersion == version && IsReady)
                return;

            await InstallCoreAsync(version, assetName, progress, cancellationToken);
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
        ProgressMessage? progress,
        CancellationToken cancellationToken)
    {
        var targetDir = PiperPlusPaths.BinaryDirectory;
        var tempDir = targetDir + ".tmp";

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);

        Directory.CreateDirectory(tempDir);

        try
        {
            await DownloadAndExtractAsync(version, assetName, tempDir, progress, cancellationToken);
            CommitInstall(version, targetDir, tempDir);
            progress?.Report(1.0, Texts.BinaryReady);
        }
        catch
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { }

            throw;
        }
    }

    static async Task DownloadAndExtractAsync(
        string version,
        string assetName,
        string tempDir,
        ProgressMessage? progress,
        CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(tempDir, assetName);
        var downloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{version}/{assetName}";
        var client = HttpClientFactory.Client;

        await ProgressiveIo.DownloadFileAsync(client, downloadUrl, zipPath,
            startFraction: 0.0, endFraction: 0.9,
            Texts.DownloadingBinary, progress, cancellationToken);

        await ProgressiveIo.ExtractZipAsync(zipPath, tempDir,
            startFraction: 0.9, endFraction: 1.0,
            Texts.ExtractingBinary, progress, cancellationToken);

        File.Delete(zipPath);
    }

    static void CommitInstall(
        string version,
        string targetDir,
        string tempDir)
    {
        var extractedRoot = Directory.EnumerateFiles(tempDir, ExecutableName, SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault()
            ?? throw new FileNotFoundException(
                $"Piper Plus CLI executable not found after extraction in '{tempDir}'.");

        try
        {
            Directory.CreateDirectory(targetDir);

            CopyDirectory(extractedRoot, targetDir);

            var newExecutable = Path.Combine(targetDir, ExecutableName);
            if (!File.Exists(newExecutable))
                throw new FileNotFoundException(
                    $"Piper Plus CLI executable not found after install in '{targetDir}'.");

            File.WriteAllText(InstalledVersionFilePath, version);
            try { Directory.Delete(tempDir, recursive: true); } catch { }

        }
        catch
        {
            throw;
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destinationDir, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destinationPath = Path.Combine(destinationDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
