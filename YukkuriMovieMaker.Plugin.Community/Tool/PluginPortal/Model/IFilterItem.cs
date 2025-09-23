using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Plugin.Update;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    public interface IFilterItem : INotifyPropertyChanged
    {
        /// <summary>
        /// フィルターの表示名
        /// </summary>
        string Name { get; }

        /// <summary>
        /// フィルターが選択されているかどうか
        /// </summary>
        bool IsChecked { get; set; }

        /// <summary>
        /// フィルターの種類
        /// </summary>
        FilterType FilterType { get; }

        /// <summary>
        /// フィルターが適用可能な状態かどうか
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// フィルターを適用する
        /// </summary>
        /// <param name="item">プラグイン</param>
        /// <returns>一致するならtrue, それ以外はfalse</returns>
        bool ApplyFilter(PluginCatalogItem item);
    }
}
