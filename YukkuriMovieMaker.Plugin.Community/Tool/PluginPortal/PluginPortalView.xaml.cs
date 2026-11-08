using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    public partial class PluginPortalView : UserControl
    {
        public PluginPortalView()
        {
            InitializeComponent();
            DataContext = new PluginPortalViewModel();
        }
    }
}
