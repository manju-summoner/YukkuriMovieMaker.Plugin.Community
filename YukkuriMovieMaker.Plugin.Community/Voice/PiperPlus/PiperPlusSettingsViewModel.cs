using System.Collections.ObjectModel;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettingsViewModel : Bindable
{
    bool isLoading;
    string versionText = string.Empty;
    string speakerCountText = string.Empty;
    ObservableCollection<PiperModelViewModel> models = [];

    public bool IsLoading
    {
        get => isLoading;
        private set => Set(ref isLoading, value);
    }

    public string VersionText
    {
        get => versionText;
        private set => Set(ref versionText, value);
    }

    public string SpeakerCountText
    {
        get => speakerCountText;
        private set => Set(ref speakerCountText, value);
    }

    public ObservableCollection<PiperModelViewModel> Models
    {
        get => models;
        private set => Set(ref models, value);
    }

    public ICommand RefreshCommand { get; }

    public PiperPlusSettingsViewModel()
    {
        RefreshCommand = new ActionCommand(_ => !IsLoading, async _ => await RefreshAsync());
        UpdateModelViewModels();
        UpdateVersionAndSpeakerText();
    }

    void UpdateModelViewModels()
    {
        Models = new ObservableCollection<PiperModelViewModel>(
            PiperSpeakerLoader.Models.Select(m => new PiperModelViewModel(m)));
    }

    async Task RefreshAsync()
    {
        await ExecuteLoadingOperationAsync(ReloadModelsAsync);
    }

    async Task ExecuteLoadingOperationAsync(Func<Task> operation)
    {
        IsLoading = true;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            VersionText = ex.Message;
            SpeakerCountText = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    async Task ReloadModelsAsync()
    {
        await Task.Run(PiperSpeakerLoader.Reload);
        UpdateModelViewModels();
        UpdateVersionAndSpeakerText();
    }

    void UpdateVersionAndSpeakerText()
    {
        if (!PiperBinaryResource.IsReady)
        {
            VersionText = Texts.BinaryNotFound;
            SpeakerCountText = string.Empty;
            return;
        }

        VersionText = PiperBinaryResource.Version;
        SpeakerCountText = string.Format(Texts.SpeakerCount, PiperSpeakerLoader.Speakers.Count);
    }
}
