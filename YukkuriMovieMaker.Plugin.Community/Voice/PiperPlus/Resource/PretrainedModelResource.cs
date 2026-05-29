using System.ComponentModel;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal sealed class PretrainedModelResource(PretrainedModelCatalogItem item) : IVoiceResource
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? DownloadStarted;

    public string Name => item.ModelName;
    public string Terms => Texts.ResourceTerms;
    public bool IsDownloaded =>
        PiperBinaryResource.IsReady && File.Exists(item.ModelPath) && File.Exists(item.ConfigPath);
    public string? FileSize => null;

    public Task<bool> HasUpdateAsync() => Task.FromResult(!IsDownloaded);

    public async Task DownloadAsync(ProgressMessage progress)
    {
        DownloadStarted?.Invoke(this, EventArgs.Empty);

        var modelRateFrom = 0.0;
        if (!PiperBinaryResource.IsReady)
        {
            await PiperBinaryResource.InstallAsync(
                PiperBinaryResource.Version, progress.GetChildProgress(0.0, 0.5));
            modelRateFrom = 0.5;
        }

        await item.DownloadAsync(progress.GetChildProgress(modelRateFrom, 1.0));
        await Task.Run(PiperSpeakerLoader.Reload);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloaded)));
    }
}
