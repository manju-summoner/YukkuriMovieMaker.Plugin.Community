using System.ComponentModel;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;

internal sealed class PretrainedModelResource(PretrainedModelDefinition definition) : IVoiceResource
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? DownloadStarted;

    public string Name => definition.ModelName;
    public string Terms => Texts.ResourceTerms;
    public bool IsDownloaded => File.Exists(definition.ModelPath) && File.Exists(definition.ConfigPath);
    public string? FileSize => null;

    public Task<bool> HasUpdateAsync() => Task.FromResult(!IsDownloaded);

    public async Task DownloadAsync(ProgressMessage progress)
    {
        DownloadStarted?.Invoke(this, EventArgs.Empty);

        var downloadProgress = new Progress<(double Progress, string Message)>(report =>
            progress.Report(report.Progress, report.Message));

        await PretrainedModelDownloader.DownloadAsync(definition, downloadProgress);
        await Task.Run(PiperSpeakerLoader.Reload);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDownloaded)));
    }
}
