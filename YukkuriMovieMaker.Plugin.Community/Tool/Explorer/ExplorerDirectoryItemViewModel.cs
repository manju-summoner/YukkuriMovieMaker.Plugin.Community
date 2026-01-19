using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public class ExplorerDirectoryItemViewModel : Bindable, IExplorerItemViewModel
    {
        private readonly string dir;
        int iconSize = 24;
        Task? loadIconTask;
        CancellationTokenSource? loadIconCts;
        ImageSource? icon;

        public string Path => dir;
        public string Name => System.IO.Path.GetFileName(dir);

        public ImageSource? Icon
        {
            get
            {
                if (icon is not null)
                    return icon;
                if (loadIconTask is not null)
                    return icon;
                loadIconCts = new CancellationTokenSource();
                var token = loadIconCts.Token;
                loadIconTask ??= Task.Run(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested)
                            return;
                        var loadedIcon = ShellIcon.GetIcon(Path, ShellIcon.GetIconSize(iconSize), isDirectory: false);
                        if (token.IsCancellationRequested)
                            return;
                        icon = loadedIcon;
                        OnPropertyChanged(nameof(Icon));
                    }
                    finally
                    {
                        loadIconCts = null;
                        loadIconTask = null;
                    }
                });
                return icon;
            }
        }
        public ImageSource? Thumbnail => null;
        public bool IsSelected { get; set => Set(ref field, value); } = false;

        public ICommand ClearCacheCommand { get; }

        public ExplorerDirectoryItemViewModel(string dir)
        {
            this.dir = dir;
            ClearCacheCommand = new ActionCommand(
                _=> true,
                _=> ClearIcon());
        }

        public void SetImageSize(int iconSize, int thumbnailSize)
        {
            if (this.iconSize != iconSize)
            {
                this.iconSize = iconSize;
                icon = null;
                OnPropertyChanged(nameof(Icon));
            }

        }

        void ClearIcon()
        {
            loadIconCts?.Cancel();
            loadIconTask = null;
            icon = null;
        }
    }
}
