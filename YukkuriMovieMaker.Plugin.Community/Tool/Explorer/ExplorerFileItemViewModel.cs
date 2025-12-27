using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public class ExplorerFileItemViewModel : Bindable, IExplorerItemViewModel
    {
        static readonly SemaphoreSlim semaphore = new(1);
        int iconSize = 24, thumbnailSize = 300;
        Task? loadIconTask, loadThumbnailTask;
        CancellationTokenSource? loadIconCts, loadThumbnailCts;
        ImageSource? icon, thumbnail;
        bool isFailedToLoadThumbnail = false;

        public string Path { get; }
        public string Name => System.IO.Path.GetFileName(Path);

        public ImageSource? Icon
        {
            get
            {
                if (icon is not null && icon.Width == iconSize)
                    return icon;
                loadIconCts = new CancellationTokenSource();
                var token = loadIconCts.Token;
                loadIconTask ??= Task.Run(() =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    var loadedIcon = ShellIcon.GetIcon(Path, ShellIcon.GetIconSize(iconSize), isDirectory: false);
                    if (token.IsCancellationRequested)
                        return;
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        icon = loadedIcon;
                        OnPropertyChanged(nameof(Icon));
                    });
                });
                return icon;
            }
        }
        public ImageSource? Thumbnail
        {
            get
            {
                if (isFailedToLoadThumbnail)
                    return null;
                if (thumbnail is not null && thumbnail.Width == thumbnailSize)
                    return thumbnail;
                loadThumbnailCts = new CancellationTokenSource();
                var token = loadThumbnailCts.Token;
                loadThumbnailTask ??= Task.Run(async () =>
                {
                    BitmapSource? loadedThumbnail = null;
                    await semaphore.WaitAsync();
                    try
                    {
                        if (token.IsCancellationRequested)
                            return;
                        loadedThumbnail = ShellThumbnail.GetThumbnailFromFactory(Path, thumbnailSize, thumbnailSize);
                        loadedThumbnail?.Freeze();
                        if (loadedThumbnail is null)
                        {
                            isFailedToLoadThumbnail = true;
                            return;
                        }

                        if (token.IsCancellationRequested)
                            return;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    thumbnail = loadedThumbnail;
                    OnPropertyChanged(nameof(Thumbnail));
                });
                return thumbnail;
            }
        }
        public bool IsSelected { get => field; set => Set(ref field, value); } = false;

        public ICommand ClearCacheCommand { get; }

        public ExplorerFileItemViewModel(string path)
        {
            Path = path;
            ClearCacheCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    ClearIcon();
                    ClearThumbnail();
                });
        }

        public void SetImageSize(int icon, int thumbnail)
        {
            if (iconSize != icon)
            {
                iconSize = icon;
                OnPropertyChanged(nameof(Icon));
            }
            if (thumbnailSize != thumbnail)
            {
                thumbnailSize = thumbnail;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        private void ClearIcon()
        {
            loadIconCts?.Cancel();
            loadIconTask = null;
            icon = null;
        }

        private void ClearThumbnail()
        {
            isFailedToLoadThumbnail = false;
            loadThumbnailCts?.Cancel();
            loadThumbnailTask = null;
            thumbnail = null;
        }
    }
}
