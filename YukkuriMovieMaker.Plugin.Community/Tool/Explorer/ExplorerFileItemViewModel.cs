using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerFileItemViewModel : Bindable, IExplorerItemViewModel
    {
        string path;
        int iconSize = 24, thumbnailSize = 300;
        CancellationTokenSource? loadIconCts, loadThumbnailCts;
        ImageSource? icon, thumbnail;

        public string Path => path;
        public string Name { get; private set; }
        public bool IsDirectory => false;
        public string Extension { get; private set; }
        public DateTime LastWriteTime { get; }
        public bool SelectsNameOnlyOnRename => true;

        public ImageSource? Icon
        {
            get
            {
                if (iconSize <= 0) return null;
                if (icon != null) return icon;

                if (loadIconCts == null)
                {
                    var capturedPath = path;
                    var capturedSize = iconSize;
                    loadIconCts = new CancellationTokenSource();
                    _ = LoadIconAsync(capturedPath, capturedSize, loadIconCts.Token);
                }

                return icon;
            }
        }

        public ImageSource? Thumbnail
        {
            get
            {
                if (thumbnailSize <= 0) return null;
                if (thumbnail != null) return thumbnail;

                if (loadThumbnailCts == null)
                {
                    var capturedPath = path;
                    var capturedIconSize = iconSize;
                    var capturedThumbSize = thumbnailSize;
                    loadThumbnailCts = new CancellationTokenSource();
                    _ = LoadThumbnailAsync(capturedPath, capturedIconSize, capturedThumbSize, loadThumbnailCts.Token);
                }

                return thumbnail ?? icon;
            }
        }

        public bool IsSelected { get => field; set => Set(ref field, value); } = false;
        public bool IsRenaming { get => field; set => Set(ref field, value); } = false;
        public string RenameText { get => field; set => Set(ref field, value); } = string.Empty;
        public ICommand ClearCacheCommand { get; }

        public ExplorerFileItemViewModel(string path, DateTime lastWriteTime)
        {
            this.path = path;
            Name = System.IO.Path.GetFileName(path) ?? path;
            LastWriteTime = lastWriteTime;
            Extension = (System.IO.Path.GetExtension(path) ?? string.Empty).TrimStart('.').ToLowerInvariant();
            ClearCacheCommand = new ActionCommand(_ => true, _ =>
            {
                ClearIcon();
                ClearThumbnail();
            });
        }

        public void UpdatePathAndName(string newPath)
        {
            path = newPath;
            Name = System.IO.Path.GetFileName(newPath) ?? newPath;
            Extension = (System.IO.Path.GetExtension(newPath) ?? string.Empty).TrimStart('.').ToLowerInvariant();
            CancelLoadIcon();
            CancelLoadThumbnail();
            icon = null;
            thumbnail = null;
            OnPropertyChanged(nameof(Path));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Extension));
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(Thumbnail));
        }

        async Task LoadIconAsync(string capturedPath, int capturedSize, CancellationToken token)
        {
            var myCts = loadIconCts;
            try
            {
                var loadedIcon = await ShellImageLoader.LoadAsync(
                    () => ShellIcon.GetIcon(capturedPath, ShellIcon.GetIconSize(capturedSize), isDirectory: false), token);

                if (loadedIcon != null && !token.IsCancellationRequested)
                {
                    icon = loadedIcon;
                    OnPropertyChanged(nameof(Icon));
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Default.Write("ExplorerFileItemViewModel.Icon", e);
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(loadIconCts, myCts))
                    {
                        loadIconCts = null;
                        myCts?.Dispose();
                    }
                });
            }
        }

        async Task LoadThumbnailAsync(string capturedPath, int capturedIconSize, int capturedThumbSize, CancellationToken token)
        {
            var myCts = loadThumbnailCts;
            try
            {
                ImageSource? result = null;

                try
                {
                    result = await ShellImageLoader.LoadAsync(
                        () => ShellThumbnail.GetThumbnailFromFactory(capturedPath, capturedThumbSize, capturedThumbSize), token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.Default.Write("ExplorerFileItemViewModel.Thumbnail.Factory", ex);
                }

                if (result == null)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        result = await ShellImageLoader.LoadAsync(
                            () => ShellIcon.GetIcon(capturedPath, ShellIcon.GetIconSize(capturedThumbSize), isDirectory: false), token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log.Default.Write("ExplorerFileItemViewModel.Thumbnail.Icon", ex);
                    }
                }

                if (result != null && !token.IsCancellationRequested)
                {
                    thumbnail = result;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Default.Write("ExplorerFileItemViewModel.Thumbnail", e);
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(loadThumbnailCts, myCts))
                    {
                        loadThumbnailCts = null;
                        myCts?.Dispose();
                    }
                });
            }
        }

        public void SetImageSize(int iconSize, int thumbnailSize)
        {
            if (this.iconSize != iconSize)
            {
                this.iconSize = iconSize;
                CancelLoadIcon();
                icon = null;
                OnPropertyChanged(nameof(Icon));
            }
            if (this.thumbnailSize != thumbnailSize)
            {
                this.thumbnailSize = thumbnailSize;
                CancelLoadThumbnail();
                thumbnail = null;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        void CancelLoadIcon()
        {
            var cts = loadIconCts;
            loadIconCts = null;
            cts?.Cancel();
            cts?.Dispose();
        }

        void CancelLoadThumbnail()
        {
            var cts = loadThumbnailCts;
            loadThumbnailCts = null;
            cts?.Cancel();
            cts?.Dispose();
        }

        void ClearIcon()
        {
            CancelLoadIcon();
            icon = null;
            OnPropertyChanged(nameof(Icon));
        }

        void ClearThumbnail()
        {
            CancelLoadThumbnail();
            thumbnail = null;
            OnPropertyChanged(nameof(Thumbnail));
        }

        public void CancelLoad()
        {
            CancelLoadIcon();
            CancelLoadThumbnail();
        }

        public void Dispose() => CancelLoad();
    }
}
