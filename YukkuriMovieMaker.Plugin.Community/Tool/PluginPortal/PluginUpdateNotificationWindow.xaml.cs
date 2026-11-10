using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    public partial class PluginUpdateNotificationWindow : Window
    {
        internal PluginUpdateNotificationWindow(IReadOnlyList<PluginUpdateNotice> notices, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            updatesList.ItemsSource = notices;
        }

        private void OpenPluginPortal_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
