using System.Collections.ObjectModel;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Pretrained;
using YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.Resource;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus;

internal sealed class PiperPlusSettingsViewModel : Bindable
{
    bool isLoading;
    double progressValue;
    string progressMessage = string.Empty;
    bool isProgressVisible;
    string statusText = string.Empty;
    bool hasUpdate;
    string updateDescription = string.Empty;
    string pendingVersion = string.Empty;
    ObservableCollection<PiperModelViewModel> models = [];

    public bool IsLoading
    {
        get => isLoading;
        private set => Set(ref isLoading, value);
    }

    public double ProgressValue
    {
        get => progressValue;
        private set => Set(ref progressValue, value);
    }

    public string ProgressMessage
    {
        get => progressMessage;
        private set => Set(ref progressMessage, value);
    }

    public bool IsProgressVisible
    {
        get => isProgressVisible;
        private set => Set(ref isProgressVisible, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => Set(ref statusText, value);
    }

    public bool HasUpdate
    {
        get => hasUpdate;
        private set => Set(ref hasUpdate, value);
    }

    public string UpdateDescription
    {
        get => updateDescription;
        private set => Set(ref updateDescription, value);
    }

    public ObservableCollection<PiperModelViewModel> Models
    {
        get => models;
        private set => Set(ref models, value);
    }

    public IReadOnlyList<PretrainedModelEntry> PretrainedModels { get; } =
        PretrainedModelCatalog.All.Select(d => new PretrainedModelEntry(d)).ToList();

    public ICommand RefreshCommand { get; }
    public ICommand UpdateCommand { get; }

    public PiperPlusSettingsViewModel()
    {
        RefreshCommand = new ActionCommand(_ => !IsLoading, async _ => await RefreshAsync());
        UpdateCommand = new ActionCommand(_ => !IsLoading && HasUpdate, async _ => await UpdateBinaryAsync());
        BuildModelsFromSettings();
        UpdateStatusText();
    }

    void BuildModelsFromSettings()
    {
        Models = new ObservableCollection<PiperModelViewModel>(
            PiperPlusSettings.Default.SavedModels.Select(s => new PiperModelViewModel(s)));
    }

    async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            PiperUpdateChecker.InvalidateCache();

            var release = await PiperUpdateChecker.GetLatestReleaseAsync();

            if (!PiperBinaryResource.IsReady)
            {
                var version = release?.TagName;
                if (string.IsNullOrEmpty(version))
                {
                    StatusText = Texts.BinaryNotFound;
                    return;
                }
                await InstallVersionAsync(version);
            }

            await ReloadModelsAsync();
            ApplyUpdateState(release);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsLoading = false;
            IsProgressVisible = false;
            ProgressValue = 0;
            ProgressMessage = string.Empty;
        }
    }

    async Task UpdateBinaryAsync()
    {
        if (string.IsNullOrEmpty(pendingVersion))
            return;

        IsLoading = true;
        try
        {
            await InstallVersionAsync(pendingVersion);
            HasUpdate = false;
            UpdateDescription = string.Empty;
            await ReloadModelsAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsLoading = false;
            IsProgressVisible = false;
            ProgressValue = 0;
            ProgressMessage = string.Empty;
        }
    }

    async Task ReloadModelsAsync()
    {
        ProgressValue = 0;
        ProgressMessage = Texts.LoadingModels;
        IsProgressVisible = true;
        await Task.Run(PiperSpeakerLoader.Reload);
        BuildModelsFromSettings();
        foreach (var entry in PretrainedModels)
            entry.RefreshDownloadedState();
        UpdateStatusText();
    }

    async Task InstallVersionAsync(string version)
    {
        IsProgressVisible = true;
        var progress = new Progress<(double Progress, string Message)>(report =>
        {
            ProgressValue = report.Progress;
            ProgressMessage = report.Message;
        });
        await PiperBinaryResource.EnsureAsync(version, progress);
    }

    void ApplyUpdateState(GitHubReleaseInfo? release)
    {
        if (release is null)
            return;

        pendingVersion = release.TagName;
        var installed = PiperBinaryResource.InstalledVersion;
        var needsUpdate = installed is null ||
            !string.Equals(installed, release.TagName, StringComparison.OrdinalIgnoreCase);

        if (needsUpdate)
        {
            UpdateDescription = $"{Texts.UpdateAvailable}{release.TagName}";
            HasUpdate = true;
        }
        else
        {
            UpdateDescription = string.Empty;
            HasUpdate = false;
        }
    }

    void UpdateStatusText()
    {
        var installed = PiperBinaryResource.InstalledVersion;
        if (!PiperBinaryResource.IsReady)
        {
            StatusText = Texts.BinaryNotFound;
            return;
        }

        var speakerCount = PiperPlusSettings.Default.Speakers.Count;
        StatusText = installed is not null
            ? $"{installed}  |  {string.Format(Texts.SpeakerCount, speakerCount)}"
            : string.Format(Texts.SpeakerCount, speakerCount);
    }
}
