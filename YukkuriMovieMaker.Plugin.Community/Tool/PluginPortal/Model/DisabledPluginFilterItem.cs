using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    public class DisabledPluginFilterItem(int count) : Bindable, IFilterItem
    {
        private bool _isChecked;
        public string Name { get; } = $"{Texts.IsEnabled} ({count})";
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }
        public FilterType FilterType { get; } = FilterType.All;
        public bool IsEnabled => true;
        public bool ApplyFilter(PluginCatalogItem item) => IsChecked || item.IsEnabled;
    }
}
