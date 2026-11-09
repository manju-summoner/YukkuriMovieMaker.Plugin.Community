using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalViewModel : Bindable
    {
        readonly string tempPluginsDir;

        private PluginCatalog? catalog;
        private GitHubReleasesPluginSummary[] allReleases = [];
        private UpdateAvailableFilterItem? updateAvailableFilter;

        private PluginCatalogItem? selectedPlugin;
        private string? searchText;

        public ObservableCollection<PluginCatalogItem> Plugins { get; } = [];
        public ObservableCollection<IFilterItem> TypeFilters { get; } = [];

        public bool IsFiltered => TypeFilters.OfType<TypeFilterItem>().Any(f => !f.IsChecked) || updateAvailableFilter?.IsChecked == true;

        public PluginCatalogItem? SelectedPlugin
        {
            get => selectedPlugin;
            set
            {
                if (Set(ref selectedPlugin, value))
                {
                    ((ActionCommand)DownloadCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(SelectedPluginTypeText));
                    OnPropertyChanged(nameof(SelectedPluginStatus));
                    OnPropertyChanged(nameof(IsSelectedPluginDownloadableFromUpdater));
                    OnPropertyChanged(nameof(IsSelectedPluginOnGitHub));
                    OnPropertyChanged(nameof(IsSelectedPluginOnOtherSite));
                    OnPropertyChanged(nameof(SelectedPluginPageUrl));
                }
            }
        }

        private IReadOnlyDictionary<PluginCatalogItem, PluginUpdateStatus> updateStatuses =
            new Dictionary<PluginCatalogItem, PluginUpdateStatus>();

        public IReadOnlyDictionary<PluginCatalogItem, PluginUpdateStatus> UpdateStatuses
        {
            get => updateStatuses;
            private set
            {
                if (Set(ref updateStatuses, value))
                {
                    updateAvailableFilter?.Count = value.Values.Count(s => s.HasUpdate);
                    ((ActionCommand)DownloadCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(SelectedPluginStatus));
                    OnPropertyChanged(nameof(IsSelectedPluginDownloadableFromUpdater));
                    OnPropertyChanged(nameof(IsSelectedPluginOnGitHub));
                    OnPropertyChanged(nameof(IsSelectedPluginOnOtherSite));
                    OnPropertyChanged(nameof(SelectedPluginPageUrl));
                }
            }
        }

        public string? SelectedPluginTypeText =>
            SelectedPlugin is null
                ? null
                : string.Join(", ", Enum.GetValues<PluginType>()
                    .Where(type => SelectedPlugin.Type.HasFlag(type))
                    .Select(type => EnumEx.GetDisplayName(type)));

        public PluginUpdateStatus? SelectedPluginStatus =>
            SelectedPlugin is null ? null : UpdateStatuses.GetValueOrDefault(SelectedPlugin);

        public bool IsSelectedPluginDownloadableFromUpdater =>
            SelectedPluginStatus?.DownloadableUpdate is not null;

        public bool IsSelectedPluginOnGitHub =>
            PluginUpdateChecker.CanDownloadFromGitHub(SelectedPlugin, SelectedPluginStatus);

        public bool IsSelectedPluginOnOtherSite =>
            SelectedPlugin is not null && !IsSelectedPluginDownloadableFromUpdater && !IsSelectedPluginOnGitHub && !string.IsNullOrEmpty(SelectedPluginPageUrl);

        public string? SelectedPluginPageUrl =>
            SelectedPluginStatus?.PageUrl ?? SelectedPlugin?.Url;

        public string? SearchText
        {
            get => searchText;
            set
            {
                if (Set(ref searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private string statusMessage = "";
        public string StatusMessage { get => statusMessage; set => Set(ref statusMessage, value); }

        private bool isInstallReady;
        public bool IsInstallReady
        {
            get => isInstallReady;
            set
            {
                if (Set(ref isInstallReady, value))
                {
                    ((ActionCommand)InstallLocalCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand DownloadCommand { get; }
        public ICommand InstallLocalCommand { get; }

        public PluginPortalViewModel()
        {
            tempPluginsDir = Path.Combine(AppDirectories.TemporaryDirectory, "plugins");
            Directory.CreateDirectory(tempPluginsDir);

            Task.Run(LoadPluginsAsync);

            DownloadCommand = new ActionCommand(
                _ => IsSelectedPluginDownloadableFromUpdater || IsSelectedPluginOnGitHub,
                async _ =>
                {
                    if (SelectedPluginStatus?.DownloadableUpdate is { } update)
                        await DownloadFromUpdaterAsync(update);
                    else
                        await DownloadPluginAsync();
                });

            InstallLocalCommand = new ActionCommand(
                _ => IsInstallReady,
                _ => InstallLocalPlugins());

            UpdateInstallReadyState();
        }

        private async Task LoadPluginsAsync()
        {
            try
            {
                catalog = await PluginPortalDataCache.GetCatalogAsync();

                var githubList = await PluginPortalDataCache.GetReleasesAsync();
                allReleases = githubList;

                var statuses = PluginUpdateChecker.CheckWithReleaseList(catalog.Plugins, githubList, out var otherSiteUpdaters);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach(var filter in TypeFilters)
                        filter.PropertyChanged -= FilterItem_PropertyChanged;
                    TypeFilters.Clear();

                    foreach (var type in Enum.GetValues<PluginType>())
                    {
                        var typeCount = catalog.Plugins.Count(p => p.Type.HasFlag(type));
                        if (typeCount == 0)
                            continue;
                        var typeFilter = new TypeFilterItem(type, typeCount);
                        typeFilter.PropertyChanged += FilterItem_PropertyChanged;
                        TypeFilters.Add(typeFilter);
                    }

                    var disabledPluginCount = catalog.Plugins.Count(p => !p.IsEnabled);
                    var isEnabledFilter =  new DisabledPluginFilterItem(disabledPluginCount);
                    isEnabledFilter.PropertyChanged += FilterItem_PropertyChanged;
                    TypeFilters.Add(isEnabledFilter);

                    updateAvailableFilter = new UpdateAvailableFilterItem(HasUpdate);
                    updateAvailableFilter.PropertyChanged += FilterItem_PropertyChanged;
                    TypeFilters.Add(updateAvailableFilter);
                    OnPropertyChanged(nameof(IsFiltered));

                    UpdateStatuses = statuses;

                    ApplyFilter();
                });

                if (otherSiteUpdaters.Count > 0)
                {
                    var results = await Task.WhenAll(otherSiteUpdaters.Select(async x =>
                        (x.Plugin, LatestUpdate: await PluginPortalDataCache.GetLatestUpdateAsync(x.Updater))));

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var updated = new Dictionary<PluginCatalogItem, PluginUpdateStatus>(UpdateStatuses);
                        foreach (var (plugin, latestUpdate) in results)
                        {
                            if (updated.TryGetValue(plugin, out var status))
                                updated[plugin] = PluginUpdateChecker.WithLatestUpdate(status, latestUpdate);
                        }
                        UpdateStatuses = updated;
                        ApplyFilter();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading plugins: {ex.Message}");
            }
        }

        private void FilterItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TypeFilterItem.IsChecked))
            {
                OnPropertyChanged(nameof(IsFiltered));
                ApplyFilter();
            }
        }

        private bool HasUpdate(PluginCatalogItem plugin) => UpdateStatuses.GetValueOrDefault(plugin)?.HasUpdate ?? false;

        private void ApplyFilter()
        {
            var previouslySelected = selectedPlugin;

            var activeFilters = TypeFilters.Where(f => f.IsEnabled);
            var filteredPlugins = 
                (catalog?.Plugins ?? [])
                .Where(p => activeFilters.Where(x => x.FilterType is FilterType.Any).Any(f => f.ApplyFilter(p)))
                .Where(p => activeFilters.Where(x => x.FilterType is FilterType.All).All(f => f.ApplyFilter(p)))
                .Where(p => 
                {
                    if(string.IsNullOrEmpty(SearchText))
                        return true;
                    var targets = new[] { p.Name, p.Author, p.Description };
                    return targets.OfType<string>().Any(text => text.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                })
                .OrderByDescending(HasUpdate);

            Plugins.Clear();
            foreach (var plugin in filteredPlugins)
            {
                Plugins.Add(plugin);
            }

            if (previouslySelected != null && Plugins.Contains(previouslySelected))
            {
                SelectedPlugin = previouslySelected;
            }
            else
            {
                SelectedPlugin = Plugins.FirstOrDefault();
            }
        }

        private async Task DownloadFromUpdaterAsync(IPluginUpdateInfo update)
        {
            //Updaterは保存先へ直接書き込むことがあり、途中で失敗した不完全なファイルをインストールの対象に残さないため、別のフォルダーに落としてから移す
            var downloadDir = Path.Combine(AppDirectories.TemporaryDirectory, "plugin-downloads", Guid.NewGuid().ToString("N"));
            try
            {
                StatusMessage = string.Format(Texts.DownloadingFile, update.DisplayName);
                Directory.CreateDirectory(downloadDir);
                var downloadedPath = await Task.Run(() => update.DownloadAsync(downloadDir, new ProgressMessage(), CancellationToken.None));
                if (string.IsNullOrEmpty(downloadedPath)
                    || !File.Exists(downloadedPath)
                    || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(downloadedPath)), Path.GetFullPath(downloadDir), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetExtension(downloadedPath), ".ymme", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = Texts.NoYmmeFileFoundInTheLatestRelease;
                    return;
                }
                var fileName = Path.GetFileName(downloadedPath);
                File.Move(downloadedPath, Path.Combine(tempPluginsDir, fileName), true);
                UpdateInstallReadyState();
                StatusMessage = string.Format(Texts.DownloadCompleted, fileName);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Texts.ErrorMessage, ex.Message);
            }
            finally
            {
                try
                {
                    Directory.Delete(downloadDir, true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete plugin download directory: {ex.Message}");
                }
            }
        }

        private async Task DownloadPluginAsync()
        {
            var githubUrl = PluginUpdateChecker.FindGitHubRepositoryUrl(SelectedPlugin);
            if (githubUrl is null) return;

            if (!PluginUpdateChecker.TryParseGitHubRepository(githubUrl, out var owner, out var repo))
            {
                StatusMessage = Texts.InvalidGitHubURL;
                return;
            }

            StatusMessage = Texts.FetchingLatestRelease;

            try
            {
                var latestRelease = allReleases
                    .Where(r => !r.Prerelease &&
                        r.User.Equals(owner, StringComparison.OrdinalIgnoreCase) && 
                        r.Repo.Equals(repo, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.PublishedAt)
                    .FirstOrDefault();
                
                if (latestRelease is null)
                {
                    StatusMessage = Texts.NoYmmeFileFoundInTheLatestRelease;
                    return;
                }

                StatusMessage = string.Format(Texts.DownloadingFile, latestRelease.FileName);

                var client = HttpClientFactory.Client;
                using var request = new HttpRequestMessage(HttpMethod.Get, latestRelease.BrowserDownloadUrl);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var savePath = Path.Combine(tempPluginsDir, latestRelease.FileName);
                var tmpPath = Path.Combine(tempPluginsDir, latestRelease.FileName+".tmp");
                try
                {
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using (var fileStream = File.Create(tmpPath))
                        await contentStream.CopyToAsync(fileStream);
                    File.Copy(tmpPath, savePath, true);
                }
                finally
                {
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                }

                UpdateInstallReadyState();
                StatusMessage = string.Format(Texts.DownloadCompleted, latestRelease.FileName);
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode is System.Net.HttpStatusCode code)
                {
                    StatusMessage = string.Format(Texts.FailedToFetchReleaseInfo, (int)code); ;
                }
                else
                {
                    StatusMessage = Texts.NetworkError;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Texts.ErrorMessage, ex.Message);
            }
        }

        private void LaunchInstaller(string path, bool isDirectory, bool cleanAfterInstall)
        {
            try
            {
                var installerPath = Path.Combine(
                    AppDirectories.ResourceDirectory,
                    "bin",
                    "Installer",
                    "YukkuriMovieMaker.Plugin.Installer.exe");

                var arguments = new List<string>();

                if (isDirectory)
                {
                    arguments.Add($"--dir");
                }

                if (cleanAfterInstall)
                {
                    arguments.Add("--clean");
                }

                arguments.Add($"\"{path}\"");

                Process.Start(installerPath, string.Join(" ", arguments));
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Texts.FailedToLaunchInstaller, ex.Message);
            }
        }

        private void UpdateInstallReadyState()
        {
            try
            {
                if (!Directory.Exists(tempPluginsDir))
                {
                    IsInstallReady = false;
                    return;
                }
                var ymmeFiles = Directory.GetFiles(tempPluginsDir, "*.ymme");
                IsInstallReady = ymmeFiles.Length > 0;
            }
            catch
            {
                IsInstallReady = false;
            }
        }

        private void InstallLocalPlugins()
        {
            if (!IsInstallReady)
            {
                MessageBox.Show(Texts.NoDownloadablePlugins, Texts.PluginPortal, MessageBoxButton.OK);
                return;
            }

            var ymmeFiles = Directory.GetFiles(tempPluginsDir, "*.ymme");

            var fileNames = ymmeFiles.Select(Path.GetFileName);

            var selectionWindow = new PluginSelectionWindow(ymmeFiles);
            var dialogResult = selectionWindow.ShowDialog();

            UpdateInstallReadyState();

            if (dialogResult != true)
            {
                StatusMessage = Texts.CancelBulkInstallation;
                return;
            }

            var selectedFiles = selectionWindow.SelectedFiles.ToList();
            if (selectedFiles.Count == 0)
            {
                StatusMessage = Texts.NoPluginsSelected;
                return;
            }
            else if (selectedFiles.Count < ymmeFiles.Length)
            {
                foreach (var file in selectedFiles)
                {
                    LaunchInstaller(file, false, PluginPortalSettings.Default.IsCleanYmmeFile);
                }
            }
            else
            {
                LaunchInstaller(tempPluginsDir, true, PluginPortalSettings.Default.IsCleanYmmeFile);
            }


            if (!PluginPortalSettings.Default.IsCleanYmmeFile)
            {
                if (!string.IsNullOrWhiteSpace(PluginPortalSettings.Default.YmmeFilePath))
                {
                    try
                    {
                        Directory.CreateDirectory(PluginPortalSettings.Default.YmmeFilePath);

                        foreach (var sourceFile in ymmeFiles)
                        {
                            string fileName = Path.GetFileName(sourceFile);
                            string destinationFile = Path.Combine(PluginPortalSettings.Default.YmmeFilePath, fileName);

                            File.Move(sourceFile, destinationFile, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(Texts.ErrorMessage, ex.Message), Texts.PluginPortal, MessageBoxButton.OK);
                    }
                }
            }
        }
    }
}
