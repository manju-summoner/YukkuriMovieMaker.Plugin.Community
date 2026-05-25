using System.IO;
using System.Net.Http;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal static class PretrainedModelDownloader
{
    public static async Task DownloadAsync(
        PretrainedModelDefinition definition,
        IProgress<(double Progress, string Message)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modelRoot = PiperPlusSettings.Default.ModelDirectory;
        var targetDir = Path.Combine(modelRoot, definition.SubDirectory);
        Directory.CreateDirectory(targetDir);

        var onnxPath = Path.Combine(targetDir, definition.OnnxFileName);
        var jsonPath = onnxPath + ".json";

        var client = HttpClientFactory.Client;

        await DownloadFileAsync(client, definition.OnnxUrl, onnxPath,
            startFraction: 0.0, endFraction: 0.9, progress, cancellationToken);

        await DownloadFileAsync(client, definition.ConfigUrl, jsonPath,
            startFraction: 0.9, endFraction: 1.0, progress, cancellationToken);

        progress?.Report((1.0, Texts.LoadingModels));
    }

    static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string destinationPath,
        double startFraction,
        double endFraction,
        IProgress<(double Progress, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(destinationPath);
        var tempPath = destinationPath + ".tmp";
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

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
                    progress?.Report((overall, string.Format(Texts.DownloadingPretrainedModel, fileName)));
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
}
