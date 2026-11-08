using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal class UpdateAvailableFilterItem(Func<PluginCatalogItem, bool> hasUpdate) : Bindable, IFilterItem
    {
        private bool isChecked;
        private int count;

        public string Name => $"{Texts.UpdateAvailable} ({count})";

        public int Count
        {
            get => count;
            set
            {
                if (Set(ref count, value))
                    OnPropertyChanged(nameof(Name));
            }
        }

        public bool IsChecked
        {
            get => isChecked;
            set => Set(ref isChecked, value);
        }
        public FilterType FilterType { get; } = FilterType.All;
        public bool IsEnabled => true;
        public bool ApplyFilter(PluginCatalogItem item) => !IsChecked || hasUpdate(item);
    }
}
