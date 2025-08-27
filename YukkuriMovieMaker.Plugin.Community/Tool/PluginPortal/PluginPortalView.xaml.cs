using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    /// <summary>
    /// PluginPortalView.xaml の相互作用ロジック
    /// </summary>
    public partial class PluginPortalView : UserControl
    {
        public PluginPortalView()
        {
            InitializeComponent();
            this.DataContext = new PluginPortalViewModel();
        }
    }
}
