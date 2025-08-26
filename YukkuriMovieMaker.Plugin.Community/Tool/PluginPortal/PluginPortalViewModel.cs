using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalViewModel : INotifyPropertyChanged
    {
        private const string YamlUrl = "https://manjubox.net/ymm4plugins.yml";
        private static readonly HttpClient httpClient = new();

        readonly string ymmesDir = @"user\ymmes";

        private List<PluginInfo> _allPlugins = [];

        private PluginInfo? _selectedPlugin;
        private string? _searchText;
        private string _statusMessage = "";

        public ObservableCollection<PluginInfo> Plugins { get; } = [];
        public ObservableCollection<TypeFilterItem> TypeFilters { get; } = [];

        public PluginInfo? SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                if (_selectedPlugin != value)
                {
                    _selectedPlugin = value;
                    OnPropertyChanged(nameof(SelectedPlugin));
                    ((ActionCommand)DownloadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string? SearchText
        {
            get => _searchText;
            set
            {
                if(_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    ApplyFilter();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                }
            }
        }

        public ICommand DownloadCommand { get; }
        public ICommand InstallLocalCommand { get; }
        public ICommand ClearCommand { get; }

        public PluginPortalViewModel()
        {
            // GitHub APIはUser-Agentヘッダーが必須のよう
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YukkuriMovieMaker4PluginPortal", "1.0"));

            Task.Run(LoadPluginsAsync);

            DownloadCommand = new ActionCommand(
                _ => SelectedPlugin?.Url?.Contains("github.com") ?? false,
                async _ => await DownloadPluginAsync());

            InstallLocalCommand = new ActionCommand(
                _ => CanInstallLocalPlugins(),
                _ => InstallLocalPlugins());

            ClearCommand = new ActionCommand(
                _ => CanInstallLocalPlugins(),
                _ => ClearLocalPlugins());
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

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var types = _allPlugins
                       .Where(p => !string.IsNullOrEmpty(p.Type))   // Typeが空でないものだけを対象
                       .SelectMany(p => p.Type.Split(','))          // カンマで分割し、リストをフラット化
                       .Select(t => t.Trim())                       // 前後の空白を削除
                       .Where(t => !string.IsNullOrEmpty(t))        // 空になった項目を除外
                       .Distinct()                                  // 重複を削除
                       .OrderBy(t => t == "その他")                 // その他を最後に
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
            if (SelectedPlugin?.Url is null) return;

            var match = Regex.Match(SelectedPlugin.Url, @"github\.com/([^/]+)/([^/]+)");
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
                var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                var response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                if (release is null)
                {
                    StatusMessage = Texts.FailedToParseReleaseInfo;
                    return;
                }

                var ymmeAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".ymme", StringComparison.OrdinalIgnoreCase));
                if (ymmeAsset is null)
                {
                    StatusMessage = Texts.NoYmmeFileFoundInTheLatestRelease;
                    return;
                }

                StatusMessage = string.Format(Texts.DownloadingFile, ymmeAsset.Name);
                var fileBytes = await httpClient.GetByteArrayAsync(ymmeAsset.BrowserDownloadUrl);

                //一時的にymmeを保存する場所
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string pluginsDir = Path.Combine(baseDir, ymmesDir);
                Directory.CreateDirectory(pluginsDir);
                var savePath = Path.Combine(pluginsDir, ymmeAsset.Name);

                await File.WriteAllBytesAsync(savePath, fileBytes);

                ((ActionCommand)InstallLocalCommand).RaiseCanExecuteChanged();
                ((ActionCommand)ClearCommand).RaiseCanExecuteChanged();
                StatusMessage = string.Format(Texts.DownloadCompleted, ymmeAsset.Name);
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

        private void InstallPlugin(string ymmeFilePath)
        {
            try
            {
                var installerPath = Path.Combine(
                    AppDirectories.ResourceDirectory,
                    "bin",
                    "Installer",
                    "YukkuriMovieMaker.Plugin.Installer.exe");

                Process.Start(installerPath, $"\"{ymmeFilePath}\"");
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Texts.FailedToLaunchInstaller, ex.Message);
            }
        }

        private bool CanInstallLocalPlugins()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string pluginsDir = Path.Combine(baseDir, ymmesDir);
                if (!Directory.Exists(pluginsDir)) return false;
                var ymmeFiles = Directory.GetFiles(pluginsDir, "*.ymme");
                return ymmeFiles.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void InstallLocalPlugins()
        {
            string baseDir = AppContext.BaseDirectory;
            string pluginsDir = Path.Combine(baseDir, ymmesDir);

            if (!CanInstallLocalPlugins())
            {
                MessageBox.Show(Texts.NoDownloadablePlugins, Texts.PluginPortal, MessageBoxButton.OK);
                return;
            }

            var ymmeFiles = Directory.GetFiles(pluginsDir, "*.ymme");

            var message = string.Format(Texts.InstallAllMessage, ymmeFiles.Length).Replace("\\n", Environment.NewLine);
            var result = MessageBox.Show(message, Texts.InstallAllPlugins, MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
            {
                StatusMessage = Texts.CancelBulkInstallation;
                return;
            }

            foreach (var filePath in ymmeFiles)
            {
                InstallPlugin(filePath);
            }

            Application.Current.Shutdown();
        }

        private void ClearLocalPlugins()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string pluginsDir = Path.Combine(baseDir, ymmesDir);

                if (!Directory.Exists(pluginsDir)) return;

                var ymmeFiles = Directory.GetFiles(pluginsDir, "*.ymme");

                if (ymmeFiles.Length == 0)
                {
                    StatusMessage = Texts.NoFilesToClear;
                    return;
                }

                var result = MessageBox.Show(
                    string.Format(Texts.ClearAllPlugins, ymmeFiles.Length).Replace("\\n", Environment.NewLine),
                    Texts.PluginPortal,
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    StatusMessage =Texts.CancelClear;
                    return;
                }

                foreach (var filePath in ymmeFiles)
                {
                    File.Delete(filePath);
                }

                StatusMessage = string.Format(Texts.DeletedFiles, ymmeFiles.Length);

                ((ActionCommand)InstallLocalCommand).RaiseCanExecuteChanged();
                ((ActionCommand)ClearCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Texts.ErrorMessage,ex.Message);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
