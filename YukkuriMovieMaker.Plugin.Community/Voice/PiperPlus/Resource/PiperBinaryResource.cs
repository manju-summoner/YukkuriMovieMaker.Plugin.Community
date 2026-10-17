using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PiperBinaryResource
{
    public const string Version = "v2.0.2";

    const string ExecutableName = "PiperPlus.Cli.exe";
    const string RepoOwner = "ayutaz";
    const string RepoName = "piper-plus";
    const string InstalledVersionFileName = "installed_version.txt";

    static readonly SemaphoreSlim InstallGate = new(1, 1);

    public static string? InstalledVersion => GetInstalledVersion(PiperPlusPaths.BinaryDirectory);

    static string? GetInstalledVersion(string binaryDirectory)
    {
        var path = Path.Combine(binaryDirectory, InstalledVersionFileName);
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

    public static string ExecutablePath => Path.Combine(PiperPlusPaths.BinaryDirectory, ExecutableName);

    public static bool IsReady => IsReadyIn(PiperPlusPaths.BinaryDirectory, Version);

    internal static bool IsReadyIn(string binaryDirectory, string version) =>
        IsBinaryReadyIn(binaryDirectory, version)
        && OpenJTalkDictionaryResource.IsValid(PiperPlusPaths.GetDictionaryDirectory(binaryDirectory));

    static bool IsBinaryReadyIn(string binaryDirectory, string version) =>
        GetInstalledVersion(binaryDirectory) == version
        && File.Exists(Path.Combine(binaryDirectory, ExecutableName));

    public static Task InstallAsync(
        string version,
        ProgressMessage? progress = null,
        CancellationToken cancellationToken = default)
        => InstallAsync(
            version,
            PiperPlusPaths.BinaryDirectory,
            HttpClientFactory.Client,
            OpenJTalkDictionaryResource.Source,
            progress,
            cancellationToken);

    internal static async Task InstallAsync(
        string version,
        string binaryDirectory,
        HttpClient client,
        OpenJTalkDictionarySource dictionarySource,
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
            //辞書の段階だけ失敗した後の再試行や、辞書フォルダが欠けたときに、導入済みのバイナリを取り直させない
            var needsBinary = !IsBinaryReadyIn(binaryDirectory, version);
            var dictionaryDirectory = PiperPlusPaths.GetDictionaryDirectory(binaryDirectory);
            var needsDictionary = !OpenJTalkDictionaryResource.IsValid(dictionaryDirectory);
            if (!needsBinary && !needsDictionary)
                return;

            //バイナリと辞書はどちらも約23MBなので、両方必要なときは進捗を半分ずつ割り当てる
            var binaryEndFraction = (needsBinary, needsDictionary) switch
            {
                (true, true) => 0.5,
                (true, false) => 1.0,
                _ => 0.0,
            };

            if (needsBinary)
                await InstallBinaryAsync(version, assetName, binaryDirectory, client,
                    0.0, binaryEndFraction, progress, cancellationToken);

            if (needsDictionary)
                await OpenJTalkDictionaryResource.InstallAsync(client, dictionarySource, dictionaryDirectory,
                    binaryEndFraction, 1.0, progress, cancellationToken);

            progress?.Report(1.0, Texts.BinaryReady);
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

    static async Task InstallBinaryAsync(
        string version,
        string assetName,
        string targetDir,
        HttpClient client,
        double startFraction,
        double endFraction,
        ProgressMessage? progress,
        CancellationToken cancellationToken)
    {
        var tempDir = targetDir + ".tmp";

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);

        Directory.CreateDirectory(tempDir);

        try
        {
            await DownloadAndExtractAsync(version, assetName, tempDir, client,
                startFraction, endFraction, progress, cancellationToken);
            CommitInstall(version, targetDir, tempDir);
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
        HttpClient client,
        double startFraction,
        double endFraction,
        ProgressMessage? progress,
        CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(tempDir, assetName);
        var downloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{version}/{assetName}";
        var extractStartFraction = startFraction + (endFraction - startFraction) * 0.9;

        await ProgressiveIo.DownloadFileAsync(client, downloadUrl, zipPath,
            startFraction, extractStartFraction,
            Texts.DownloadingBinary, progress, cancellationToken);

        await ProgressiveIo.ExtractZipAsync(zipPath, tempDir,
            extractStartFraction, endFraction,
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

            File.WriteAllText(Path.Combine(targetDir, InstalledVersionFileName), version);
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
