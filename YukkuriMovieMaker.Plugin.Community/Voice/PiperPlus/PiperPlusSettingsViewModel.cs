using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettingsViewModel : Bindable
{
    bool isLoading;
    string statusText = string.Empty;

    public bool IsLoading
    {
        get => isLoading;
        private set => Set(ref isLoading, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => Set(ref statusText, value);
    }

    public ICommand RefreshCommand { get; }

    public PiperPlusSettingsViewModel()
    {
        RefreshCommand = new ActionCommand(_ => !IsLoading, async _ => await RefreshAsync());
        UpdateStatusText();
    }

    async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            if (!PiperBinaryResource.IsReady)
            {
                StatusText = Texts.DownloadingBinary;
                await PiperBinaryResource.EnsureAsync();
            }

            StatusText = Texts.LoadingModels;
            await Task.Run(PiperSpeakerLoader.Reload);
            UpdateStatusText();
        }
        finally
        {
            IsLoading = false;
        }
    }

    void UpdateStatusText()
    {
        StatusText = PiperBinaryResource.IsReady
            ? string.Format(Texts.SpeakerCount, PiperPlusSettings.Default.Speakers.Count)
            : Texts.BinaryNotFound;
    }
}
