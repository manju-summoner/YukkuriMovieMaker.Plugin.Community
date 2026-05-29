using System.ComponentModel;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

internal sealed class PiperBinaryVoiceResource : IVoiceResource
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? DownloadStarted;

    public string Name => Texts.BinaryResourceName;
    public string Terms => Texts.BinaryResourceTerms;
    public bool IsDownloaded => PiperBinaryResource.IsReady;
    public string? FileSize => null;

    public Task<bool> HasUpdateAsync() => Task.FromResult(!PiperBinaryResource.IsReady);

    public async Task DownloadAsync(ProgressMessage progress)
    {
        DownloadStarted?.Invoke(this, EventArgs.Empty);

        await PiperBinaryResource.InstallAsync(PiperBinaryResource.Version, progress);
        await Task.Run(PiperSpeakerLoader.Reload);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloaded)));
    }
}
