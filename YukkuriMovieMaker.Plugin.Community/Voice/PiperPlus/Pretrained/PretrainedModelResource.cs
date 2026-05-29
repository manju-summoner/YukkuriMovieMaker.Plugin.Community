using System.ComponentModel;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal sealed class PretrainedModelResource(PretrainedModelDefinition definition) : IVoiceResource
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? DownloadStarted;

    public string Name => definition.ModelName;
    public string Terms => Texts.ResourceTerms;
    public bool IsDownloaded =>
        PiperBinaryResource.IsReady && File.Exists(definition.ModelPath) && File.Exists(definition.ConfigPath);
    public string? FileSize => null;

    public Task<bool> HasUpdateAsync() => Task.FromResult(!IsDownloaded);

    public async Task DownloadAsync(ProgressMessage progress)
    {
        DownloadStarted?.Invoke(this, EventArgs.Empty);

        if (!PiperBinaryResource.IsReady)
            await PiperBinaryResource.EnsureAsync(PiperBinaryResource.Version, progress);

        await PretrainedModelDownloader.DownloadAsync(definition, progress);
        await Task.Run(PiperSpeakerLoader.Reload);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloaded)));
    }
}
