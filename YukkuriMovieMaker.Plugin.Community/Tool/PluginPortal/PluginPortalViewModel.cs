using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal partial class PluginPortalViewModel : Bindable
    {
        private const string YamlUrl = "https://manjubox.net/ymm4plugins.yml";
        private const string ManjuboxApiUrl = "https://manjubox.net/api/ymm4plugins/github/list";
        private static readonly HttpClient httpClient = new();

        readonly string _tempPluginsDir;

        private List<PluginInfo> _allPlugins = [];
        private List<ManjuboxReleaseInfo> _allReleases = [];

        private PluginInfo? _selectedPlugin;
        private string? _searchText;

        public ObservableCollection<PluginInfo> Plugins { get; } = [];
        public ObservableCollection<TypeFilterItem> TypeFilters { get; } = [];

        public PluginInfo? SelectedPlugin
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

            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("user-agent", $"YukkuriMovieMaker_v{AppVersion.Current}"));

            Task.Run(LoadPluginsAsync);

            DownloadCommand = new ActionCommand(
                _ => FindGitHubRepositoryUrl(SelectedPlugin) is not null,
                async _ => await DownloadPluginAsync());

            InstallLocalCommand = new ActionCommand(
                _ => IsInstallReady,
                _ => InstallLocalPlugins());

            UpdateInstallReadyState();
        }

        private static string? FindGitHubRepositoryUrl(PluginInfo? plugin)
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
                var yamlContent = await httpClient.GetStringAsync(YamlUrl);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                _allPlugins = deserializer.Deserialize<List<PluginInfo>>(yamlContent);

                // GitHubのリリース一覧を取得
                var githubList = await httpClient.GetAsync(ManjuboxApiUrl);
                githubList.EnsureSuccessStatusCode();
                var json = await githubList.Content.ReadAsStringAsync();
                _allReleases = JsonSerializer.Deserialize<List<ManjuboxReleaseInfo>>(json) ?? [];


                Application.Current.Dispatcher.Invoke(() =>
                {
                    var types = _allPlugins
                       .SelectMany(p => p.Type?.Split(',') ?? [])          // カンマで分割し、リストをフラット化
                       .Select(t => t.Trim())                       // 前後の空白を削除
                       .Where(t => !string.IsNullOrEmpty(t))        // 空になった項目を除外
                       .Distinct()                                  // 重複を削除
                       .OrderBy(t => t == "その他")                 // 「その他」を最後に
                       .ThenBy(t => t);                             // それ以外を並べ替え

                    TypeFilters.Clear();
                    foreach (var type in types)
                    {
                        TypeFilters.Add(new TypeFilterItem(type, true, ApplyFilter));
                    }
                    TypeFilters.Add(new TypeFilterItem(Texts.IsEnabled, false, ApplyFilter));

                    ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading plugins: {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            var previouslySelected = _selectedPlugin;
            IEnumerable<PluginInfo> filteredPlugins = _allPlugins;

            var checkedTypes = TypeFilters.Where(f => f.IsChecked && f.Name != Texts.IsEnabled).Select(f => f.Name).ToList();
            if (checkedTypes.Count != 0)
            {
                filteredPlugins = filteredPlugins.Where(p =>
                    !string.IsNullOrEmpty(p.Type) &&
                    p.Type.Split(',').Select(t => t.Trim()).Any(t => checkedTypes.Contains(t))
                );
            }

            if (!string.IsNullOrEmpty(SearchText))
            {
                filteredPlugins = filteredPlugins.Where(p =>
                    (p.Name?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Author?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Description?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            var showDistributedEnded = TypeFilters.FirstOrDefault(f => f.Name == Texts.IsEnabled)?.IsChecked ?? false;
            if (!showDistributedEnded)
            {
                filteredPlugins = filteredPlugins.Where(p => p.IsEnabled);
            }

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
                var fileBytes = await httpClient.GetByteArrayAsync(latestRelease.BrowserDownloadUrl);

                //一時的にymmeを保存する
                var savePath = Path.Combine(_tempPluginsDir, latestRelease.FileName);
                await File.WriteAllBytesAsync(savePath, fileBytes);

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
