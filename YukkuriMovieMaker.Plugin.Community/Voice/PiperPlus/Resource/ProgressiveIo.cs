using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal static class ProgressiveIo
{
    internal static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string destinationPath,
        double startFraction,
        double endFraction,
        string message,
        IProgress<(double Progress, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = destinationPath + ".tmp";
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            progress?.Report((startFraction, message));

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(tempPath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;
                if (totalBytes > 0)
                {
                    var fileFraction = (double)downloaded / totalBytes;
                    var overall = startFraction + fileFraction * (endFraction - startFraction);
                    progress?.Report((overall, message));
                }
            }
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    internal static async Task ExtractZipAsync(
        string zipPath,
        string destinationDir,
        double startFraction,
        double endFraction,
        string message,
        IProgress<(double Progress, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report((startFraction, message));

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries;
            var total = entries.Count;
            var resolvedDestination = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = entries[i];
                var destPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));

                if (!destPath.StartsWith(resolvedDestination, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Entry '{entry.FullName}' would extract outside destination directory.");

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(destPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                var fraction = (double)(i + 1) / total;
                var overall = startFraction + fraction * (endFraction - startFraction);
                progress?.Report((overall, message));
            }
        }, cancellationToken);
    }
}
