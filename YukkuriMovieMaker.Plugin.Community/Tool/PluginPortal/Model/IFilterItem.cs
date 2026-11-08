using System.ComponentModel;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    public interface IFilterItem : INotifyPropertyChanged
    {
        string Name { get; }

        bool IsChecked { get; set; }

        FilterType FilterType { get; }

        bool IsEnabled { get; }

        bool ApplyFilter(PluginCatalogItem item);
    }
}
