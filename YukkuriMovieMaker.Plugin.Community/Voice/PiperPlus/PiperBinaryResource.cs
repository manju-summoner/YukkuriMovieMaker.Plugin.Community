using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class PiperBinaryResource
{
    const string Version = "v1.12.0";
    const string WindowsX64Asset = "piper-plus-cli-win-x64.zip";
    const string ExecutableName = "PiperPlus.Cli.exe";

    static readonly string DownloadUrl =
        $"https://github.com/ayutaz/piper-plus/releases/download/{Version}/{WindowsX64Asset}";

    static readonly EnumerationOptions SearchOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
    };

    static string InstallDirectory =>
        Path.Combine(PiperPlusSettings.Default.BinaryDirectory, Version);

    static string? resolvedExecutablePath;

    public static string? ExecutablePath
    {
        get
        {
            if (resolvedExecutablePath is not null && File.Exists(resolvedExecutablePath))
                return resolvedExecutablePath;

            var dir = InstallDirectory;
            resolvedExecutablePath = Directory.Exists(dir)
                ? Directory
                    .EnumerateFiles(dir, ExecutableName, SearchOptions)
                    .FirstOrDefault()
                : null;

            return resolvedExecutablePath;
        }
    }

    public static bool IsReady => ExecutablePath is not null;

    public static void InvalidateCache() => resolvedExecutablePath = null;

    public static async Task EnsureAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsReady)
            return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Piper Plus is only supported on Windows x64.");

        var dir = InstallDirectory;
        Directory.CreateDirectory(dir);

        var zipPath = Path.Combine(dir, WindowsX64Asset);

        var client = HttpClientFactory.Client;
        using var response = await client.GetAsync(
            DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = File.Create(zipPath))
        {
            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloadedBytes += read;
                if (totalBytes > 0)
                    progress?.Report((double)downloadedBytes / totalBytes * 0.9);
            }
        }

        await Task.Run(
            () => ZipFile.ExtractToDirectory(zipPath, dir, overwriteFiles: true),
            cancellationToken);

        try { File.Delete(zipPath); } catch { }

        resolvedExecutablePath = null;

        if (!IsReady)
            throw new FileNotFoundException(
                $"Piper Plus CLI executable not found after extraction in '{dir}'.");

        progress?.Report(1.0);
    }
}
