using System.IO;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal sealed record PretrainedModelCatalogItem(
    string OnnxFileName,
    string OnnxUrl,
    string ConfigUrl,
    string TermsUrl
)
{
    public string ModelName => Path.GetFileNameWithoutExtension(OnnxFileName);
    public string ModelPath => Path.Combine(PiperPlusPaths.ModelDirectory, OnnxFileName);
    public string ConfigPath => ModelPath + ".json";

    public async Task DownloadAsync(ProgressMessage progress)
    {
        Directory.CreateDirectory(PiperPlusPaths.ModelDirectory);

        var client = HttpClientFactory.Client;

        await ProgressiveIo.DownloadFileAsync(client, OnnxUrl, ModelPath,
            startFraction: 0.0, endFraction: 0.9,
            string.Format(Texts.DownloadingPretrainedModel, OnnxFileName),
            progress, default);

        await ProgressiveIo.DownloadFileAsync(client, ConfigUrl, ConfigPath,
            startFraction: 0.9, endFraction: 1.0,
            string.Format(Texts.DownloadingPretrainedModel, Path.GetFileName(ConfigPath)),
            progress, default);

        progress.Report(1.0, Texts.LoadingModels);
    }
}
