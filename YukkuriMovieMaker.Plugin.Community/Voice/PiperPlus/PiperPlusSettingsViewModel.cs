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
    string versionText = string.Empty;
    string speakerCountText = string.Empty;
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

    public IReadOnlyList<PretrainedModelEntry> PretrainedModels { get; }

    public ICommand RefreshCommand { get; }
    public ICommand UpdateCommand { get; }

    public PiperPlusSettingsViewModel()
    {
        PretrainedModels = PretrainedModelCatalog.All
            .Select(d => new PretrainedModelEntry(d))
            .ToList();

        foreach (var entry in PretrainedModels)
            entry.DownloadCompleted += OnPretrainedDownloadCompleted;

        RefreshCommand = new ActionCommand(_ => !IsLoading, async _ => await RefreshAsync());
        UpdateCommand = new ActionCommand(_ => !IsLoading && HasUpdate, async _ => await UpdateBinaryAsync());
        BuildModelsFromSettings();
        UpdateVersionAndSpeakerText();
        ApplyUpdateState(PiperUpdateChecker.CachedRelease);

        _ = InitializeUpdateStateAsync();
    }

    async Task InitializeUpdateStateAsync()
    {
        var release = await PiperUpdateChecker.GetLatestReleaseAsync();
        ApplyUpdateState(release);
    }

    void BuildModelsFromSettings()
    {
        Models = new ObservableCollection<PiperModelViewModel>(
            PiperPlusSettings.Default.SavedModels.Select(s => new PiperModelViewModel(s)));
    }

    async void OnPretrainedDownloadCompleted(object? sender, EventArgs e)
    {
        if (IsLoading)
            return;

        await ExecuteLoadingOperationAsync(ReloadModelsAsync);
    }

    async Task RefreshAsync()
    {
        await ExecuteLoadingOperationAsync(async () =>
        {
            await PiperUpdateChecker.InvalidateCacheAsync();

            var release = await PiperUpdateChecker.GetLatestReleaseAsync();

            if (!PiperBinaryResource.IsReady)
            {
                if (release is not { TagName: { Length: > 0 } version })
                {
                    VersionText = Texts.BinaryNotFound;
                    SpeakerCountText = string.Empty;
                    return;
                }
                await InstallVersionAsync(version);
            }

            await ReloadModelsAsync();
            ApplyUpdateState(release);
        });
    }

    async Task UpdateBinaryAsync()
    {
        if (string.IsNullOrEmpty(pendingVersion))
            return;

        await ExecuteLoadingOperationAsync(async () =>
        {
            await InstallVersionAsync(pendingVersion);
            HasUpdate = false;
            UpdateDescription = string.Empty;
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
        UpdateVersionAndSpeakerText();
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
        if (release is not { TagName: var tag })
        {
            pendingVersion = string.Empty;
            UpdateDescription = string.Empty;
            HasUpdate = false;
            return;
        }

        pendingVersion = tag;
        var installed = PiperBinaryResource.InstalledVersion;
        var needsUpdate = installed is null ||
            !string.Equals(installed, tag, StringComparison.OrdinalIgnoreCase);

        (UpdateDescription, HasUpdate) = needsUpdate
            ? ($"{Texts.UpdateAvailable} {tag}", true)
            : (string.Empty, false);
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
        SpeakerCountText = string.Format(Texts.SpeakerCount, PiperPlusSettings.Default.Speakers.Count);
    }
}
