using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal partial class PluginPortalViewModel : Bindable
    {
        readonly string _tempPluginsDir;

        private PluginCatalog? _catalog;
        private GitHubReleasesPluginSummary[] _allReleases = [];

        private PluginCatalogItem? _selectedPlugin;
        private string? _searchText;

        public ObservableCollection<PluginCatalogItem> Plugins { get; } = [];
        public ObservableCollection<IFilterItem> TypeFilters { get; } = [];

        public PluginCatalogItem? SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                if (Set(ref _selectedPlugin, value))
                {
                    ((ActionCommand)DownloadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string? SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

        private bool _isInstallReady;
        public bool IsInstallReady
        {
            get => _isInstallReady;
            set
            {
                if (Set(ref _isInstallReady, value))
                {
                    ((ActionCommand)InstallLocalCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand DownloadCommand { get; }
        public ICommand InstallLocalCommand { get; }

        public PluginPortalViewModel()
        {
            _tempPluginsDir = Path.Combine(AppDirectories.TemporaryDirectory, "plugins");
            Directory.CreateDirectory(_tempPluginsDir);

            Task.Run(LoadPluginsAsync);

            DownloadCommand = new ActionCommand(
                _ => FindGitHubRepositoryUrl(SelectedPlugin) is not null,
                async _ => await DownloadPluginAsync());

            InstallLocalCommand = new ActionCommand(
                _ => IsInstallReady,
                _ => InstallLocalPlugins());

            UpdateInstallReadyState();
        }

        private static string? FindGitHubRepositoryUrl(PluginCatalogItem? plugin)
        {
            if (plugin is null) return null;

            if (plugin.Url is not null && plugin.Url.Contains("github.com"))
            {
                return plugin.Url;
            }

            if (plugin.Links is not null)
            {
                return plugin.Links.FirstOrDefault(link => link?.Contains("github.com") ?? false);
            }

            return null;
        }

        private async Task LoadPluginsAsync()
        {
            try
            {
                _catalog = await PluginCatalog.GetPluginCatalogAsync();

                // GitHubのリリース一覧を取得
                var githubList = await GitHubReleasesPluginSummaries.GetSummariesAsync(false);
                _allReleases = githubList;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach(var filter in TypeFilters)
                        filter.PropertyChanged -= FilterItem_PropertyChanged;
                    TypeFilters.Clear();

                    foreach (var type in Enum.GetValues<PluginType>())
                    {
                        var typeCount = _catalog.Plugins.Count(p => p.Type.HasFlag(type));
                        if (typeCount == 0)
                            continue;
                        var typeFilter = new TypeFilterItem(type, typeCount);
                        typeFilter.PropertyChanged += FilterItem_PropertyChanged;
                        TypeFilters.Add(typeFilter);
                    }

                    var disabledPluginCount = _catalog.Plugins.Count(p => !p.IsEnabled);
                    var isEnabledFilter =  new DisabledPluginFilterItem(disabledPluginCount);
                    isEnabledFilter.PropertyChanged += FilterItem_PropertyChanged;
                    TypeFilters.Add(isEnabledFilter);

                    ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading plugins: {ex.Message}");
            }
        }

        private void FilterItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TypeFilterItem.IsChecked))
                ApplyFilter();
        }

        private void ApplyFilter()
        {
            var previouslySelected = _selectedPlugin;

            var activeFilters = TypeFilters.Where(f => f.IsEnabled);
            var filteredPlugins = 
                (_catalog?.Plugins ?? [])
                .Where(p => activeFilters.Where(x => x.FilterType is FilterType.Any).Any(f => f.ApplyFilter(p)))
                .Where(p => activeFilters.Where(x => x.FilterType is FilterType.All).All(f => f.ApplyFilter(p)))
                .Where(p => 
                {
                    if(string.IsNullOrEmpty(SearchText))
                        return true;
                    var targets = new[] { p.Name, p.Author, p.Description };
                    return targets.OfType<string>().Any(text => text.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                });

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

        private async Task DownloadPluginAsync()
        {
            var githubUrl = FindGitHubRepositoryUrl(SelectedPlugin);
            if (githubUrl is null) return;

            var match = GitHubRepositoryUrlRegex().Match(githubUrl);
            if (!match.Success)
            {
                StatusMessage = Texts.InvalidGitHubURL;
                return;
            }

            var owner = match.Groups[1].Value;
            var repo = match.Groups[2].Value;

            StatusMessage = Texts.FetchingLatestRelease;

            try
            {
                var latestRelease = _allReleases
                    .Where(r => !r.Prerelease &&                                            // プレリリースは含めない
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
                request.Headers.UserAgent.ParseAdd($"YukkuriMovieMaker v{AppVersion.Current}");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var savePath = Path.Combine(_tempPluginsDir, latestRelease.FileName);
                var tmpPath = Path.Combine(_tempPluginsDir, latestRelease.FileName+".tmp");
                try
                {
                    //一時的にymmeを保存する
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(tmpPath);
                    await contentStream.CopyToAsync(fileStream);
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
                if (!Directory.Exists(_tempPluginsDir))
                {
                    IsInstallReady = false;
                    return;
                }
                var ymmeFiles = Directory.GetFiles(_tempPluginsDir, "*.ymme");
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

            var ymmeFiles = Directory.GetFiles(_tempPluginsDir, "*.ymme");

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
                LaunchInstaller(_tempPluginsDir, true, PluginPortalSettings.Default.IsCleanYmmeFile);
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

        [GeneratedRegex(@"github\.com/([^/]+)/([^/]+)")]
        private static partial Regex GitHubRepositoryUrlRegex();
    }
}
