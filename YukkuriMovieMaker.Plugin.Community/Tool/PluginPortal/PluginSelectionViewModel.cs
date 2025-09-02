using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginSelectionViewModel
    {
        public ObservableCollection<SelectablePluginFile> PluginFiles { get; }

        public ICommand DeleteCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CancelCommand { get; }

        private readonly Action<bool?, IEnumerable<string>> _closeAction;

        public PluginSelectionViewModel(IEnumerable<string> filePaths, Action<bool?, IEnumerable<string>> closeAction)
        {
            _closeAction = closeAction;
            PluginFiles = new ObservableCollection<SelectablePluginFile>(
                filePaths.Select(path => new SelectablePluginFile(path))
            );

            InstallCommand = new ActionCommand(
                _ => PluginFiles.Count > 0,
                _ =>
                {
                    var selectedFiles = PluginFiles
                        .Where(f => f.IsSelected)
                        .Select(f => f.FilePath)
                        .ToList();
                    _closeAction(true, selectedFiles);
                });

            CancelCommand = new ActionCommand(
                _ => true,
                _ => _closeAction(false, []));

            DeleteCommand = new ActionCommand(
                parameter => parameter is IList list && list.Count > 0,
                parameter =>
                {
                    if (parameter is not IList selectedItems) return;

                    var pluginToDelete = selectedItems.OfType<SelectablePluginFile>().ToList();

                    foreach (var plugin in pluginToDelete)
                    {
                        try
                        {
                            if (File.Exists(plugin.FilePath))
                            {
                                File.Delete(plugin.FilePath);
                            }
                            PluginFiles.Remove(plugin);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format(Texts.ErrorMessage, ex.Message));
                        }
                    }
                     ((ActionCommand)InstallCommand).RaiseCanExecuteChanged();
                });
        }
    }
}