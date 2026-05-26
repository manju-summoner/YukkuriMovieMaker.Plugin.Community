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
    internal class ExplorerDirectoryItemViewModel : Bindable, IExplorerItemViewModel
    {
        string dir;
        int iconSize = 24;
        CancellationTokenSource? loadIconCts;
        ImageSource? icon;

        public string Path => dir;
        public string Name { get; private set; }
        public bool IsDirectory => true;
        public string Extension => string.Empty;
        public DateTime LastWriteTime { get; }
        public bool SelectsNameOnlyOnRename => false;

        public ImageSource? Icon
        {
            get
            {
                if (iconSize <= 0) return null;
                if (icon != null) return icon;

                if (loadIconCts == null)
                {
                    var capturedPath = dir;
                    var capturedSize = iconSize;
                    loadIconCts = new CancellationTokenSource();
                    _ = LoadIconAsync(capturedPath, capturedSize, loadIconCts.Token);
                }

                return icon;
            }
        }

        public ImageSource? Thumbnail => null;
        public bool IsSelected { get; set => Set(ref field, value); } = false;
        public bool IsRenaming { get; set => Set(ref field, value); } = false;
        public string RenameText { get; set => Set(ref field, value); } = string.Empty;
        public ICommand ClearCacheCommand { get; }

        public ExplorerDirectoryItemViewModel(string dir, DateTime lastWriteTime)
        {
            this.dir = dir;
            LastWriteTime = lastWriteTime;
            Name = GetDirectoryName(dir);
            ClearCacheCommand = new ActionCommand(_ => true, _ => ClearIcon());
        }

        public void UpdatePathAndName(string newPath)
        {
            dir = newPath;
            Name = GetDirectoryName(newPath);
            CancelLoadOnly();
            icon = null;
            OnPropertyChanged(nameof(Path));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Icon));
        }

        static string GetDirectoryName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                return new DirectoryInfo(path).Name;
            }
            catch
            {
                return path;
            }
        }

        async Task LoadIconAsync(string capturedPath, int capturedSize, CancellationToken token)
        {
            var myCts = loadIconCts;
            try
            {
                var loadedIcon = await ShellImageLoader.LoadAsync(
                    () => ShellIcon.GetIcon(capturedPath, ShellIcon.GetIconSize(capturedSize), isDirectory: true), token);

                if (loadedIcon != null && !token.IsCancellationRequested)
                {
                    icon = loadedIcon;
                    OnPropertyChanged(nameof(Icon));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Default.Write("ExplorerDirectoryItemViewModel.Icon", e);
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

        public void SetImageSize(int iconSize, int thumbnailSize)
        {
            if (this.iconSize != iconSize)
            {
                this.iconSize = iconSize;
                CancelLoadOnly();
                icon = null;
                OnPropertyChanged(nameof(Icon));
            }
        }

        void CancelLoadOnly()
        {
            var cts = loadIconCts;
            loadIconCts = null;
            cts?.Cancel();
            cts?.Dispose();
        }

        void ClearIcon()
        {
            CancelLoadOnly();
            icon = null;
            OnPropertyChanged(nameof(Icon));
        }

        public void CancelLoad() => CancelLoadOnly();

        public void Dispose() => CancelLoad();
    }
}
