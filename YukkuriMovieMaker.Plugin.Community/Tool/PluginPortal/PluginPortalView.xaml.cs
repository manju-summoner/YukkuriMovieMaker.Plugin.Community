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

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = (sender as Button)?.ContextMenu;
            if (contextMenu == null) return;

            contextMenu.PlacementTarget = sender as UIElement;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            contextMenu.IsOpen = true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
