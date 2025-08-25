using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginPortalViewModel : INotifyPropertyChanged
    {
        private const string YamlUrl = "https://manjubox.net/ymm4plugins.yml";

        private List<PluginInfo> _allPlugins = [];

        private PluginInfo? _selectedPlugin;
        private string? _searchText;

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

        public PluginPortalViewModel()
        {
            Task.Run(LoadPluginsAsync);
        }

        private async Task LoadPluginsAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
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
                System.Diagnostics.Debug.WriteLine($"Error loading plugins: {ex.Message}");
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
