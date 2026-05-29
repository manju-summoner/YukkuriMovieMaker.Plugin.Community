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
    bool hasUpdate;
    string updateDescription = string.Empty;
    string pendingVersion = string.Empty;
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

    public ICommand RefreshCommand { get; }
    public ICommand UpdateCommand { get; }

    public PiperPlusSettingsViewModel()
    {
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
            PiperSpeakerLoader.Models.Select(m => new PiperModelViewModel(m)));
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

        if (!PiperBinaryResource.IsReady)
        {
            UpdateDescription = string.Empty;
            HasUpdate = false;
            return;
        }

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
        SpeakerCountText = string.Format(Texts.SpeakerCount, PiperSpeakerLoader.Speakers.Count);
    }
}
