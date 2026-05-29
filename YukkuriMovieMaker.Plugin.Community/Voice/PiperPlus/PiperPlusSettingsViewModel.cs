using System.Collections.ObjectModel;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettingsViewModel : Bindable
{
    bool isLoading;
    ProgressMessage progress = new();
    bool isProgressVisible;
    string versionText = string.Empty;
    string speakerCountText = string.Empty;
    ObservableCollection<PiperModelViewModel> models = [];

    public bool IsLoading
    {
        get => isLoading;
        private set => Set(ref isLoading, value);
    }

    public ProgressMessage Progress
    {
        get => progress;
        private set => Set(ref progress, value);
    }

    public bool IsProgressVisible
    {
        get => isProgressVisible;
        private set => Set(ref isProgressVisible, value);
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
        BuildModelsFromSettings();
        UpdateVersionAndSpeakerText();
    }

    void BuildModelsFromSettings()
    {
        Models = new ObservableCollection<PiperModelViewModel>(
            PiperSpeakerLoader.Models.Select(m => new PiperModelViewModel(m)));
    }

    async Task RefreshAsync()
    {
        await ExecuteLoadingOperationAsync(async () =>
        {
            if (!PiperBinaryResource.IsReady)
                await InstallVersionAsync(PiperBinaryResource.Version);

            await ReloadModelsAsync();
        });
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
            IsProgressVisible = false;
            Progress = new ProgressMessage();
        }
    }

    async Task ReloadModelsAsync()
    {
        Progress.Report(0, Texts.LoadingModels);
        IsProgressVisible = true;
        await Task.Run(PiperSpeakerLoader.Reload);
        BuildModelsFromSettings();
        UpdateVersionAndSpeakerText();
    }

    async Task InstallVersionAsync(string version)
    {
        IsProgressVisible = true;
        await PiperBinaryResource.EnsureAsync(version, Progress);
    }

    void UpdateVersionAndSpeakerText()
    {
        if (!PiperBinaryResource.IsReady)
        {
            VersionText = Texts.BinaryNotFound;
            SpeakerCountText = string.Empty;
            return;
        }

        VersionText = PiperBinaryResource.InstalledVersion ?? string.Empty;
        SpeakerCountText = string.Format(Texts.SpeakerCount, PiperSpeakerLoader.Speakers.Count);
    }
}
