using System.IO;
using System.Net.Http;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal static class PretrainedModelDownloader
{
    public static async Task DownloadAsync(
        PretrainedModelDefinition definition,
        ProgressMessage? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(PiperPlusPaths.ModelDirectory);

        var onnxPath = definition.ModelPath;
        var jsonPath = onnxPath + ".json";

        var client = HttpClientFactory.Client;

        await ProgressiveIo.DownloadFileAsync(client, definition.OnnxUrl, onnxPath,
            startFraction: 0.0, endFraction: 0.9,
            string.Format(Texts.DownloadingPretrainedModel, definition.OnnxFileName),
            progress, cancellationToken);

        await ProgressiveIo.DownloadFileAsync(client, definition.ConfigUrl, jsonPath,
            startFraction: 0.9, endFraction: 1.0,
            string.Format(Texts.DownloadingPretrainedModel, Path.GetFileName(jsonPath)),
            progress, cancellationToken);

        progress?.Report(1.0, Texts.LoadingModels);
    }
}
