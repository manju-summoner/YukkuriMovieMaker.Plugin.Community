using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public interface IExplorerItemLifecycle : IDisposable
    {
        void CancelLoad();
    }

    public interface IExplorerSelectableItem : INotifyPropertyChanged, IExplorerItemLifecycle
    {
        string Path { get; }
        string Name { get; }
        bool IsSelected { get; set; }
        bool IsRenaming { get; set; }
        string RenameText { get; set; }
        void SetImageSize(int iconSize, int thumbnailSize);
        void UpdatePathAndName(string newPath);
    }

    public interface IExplorerItemViewModel : IExplorerSelectableItem
    {
        bool IsDirectory { get; }
        string Extension { get; }
        DateTime LastWriteTime { get; }
        ImageSource? Icon { get; }
        ImageSource? Thumbnail { get; }
        bool SelectsNameOnlyOnRename { get; }
        ICommand ClearCacheCommand { get; }
    }
}
