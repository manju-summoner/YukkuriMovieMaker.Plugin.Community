using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    public class TypeFilterItem(PluginType type, int count) : Bindable, IFilterItem
    {
        private bool isChecked = true;
        public string Name { get; } = $"{EnumEx.GetDisplayName(type)} ({count})";
        
        public bool IsChecked
        {
            get => isChecked;
            set => Set(ref isChecked, value);
        }

        public FilterType FilterType { get; } = FilterType.Any;

        public bool IsEnabled => IsChecked;
        public bool ApplyFilter(PluginCatalogItem item) => item.Type.HasFlag(type);
    }
}
