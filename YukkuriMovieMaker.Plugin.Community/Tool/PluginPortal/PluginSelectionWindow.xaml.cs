using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    /// <summary>
    /// PluginSelectionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class PluginSelectionWindow : Window
    {
        public IEnumerable<string> SelectedFiles { get; private set; } = [];

        public PluginSelectionWindow(IEnumerable<string> filePaths)
        {
            InitializeComponent();

            DataContext = new PluginSelectionViewModel(filePaths, (result, selectedFiles) =>
            {
                this.SelectedFiles = selectedFiles;
                this.DialogResult = result;
                this.Close();
            });
        }

        private void PluginDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataContext is PluginSelectionViewModel viewModel)
            {
                ((ActionCommand)viewModel.DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }
}
