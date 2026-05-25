using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerSidebarDirectoryViewModel : Bindable, IExplorerSelectableItem
    {
        string path;
        string name;
        readonly int level;
        bool isExpanded;
        bool hasDummyChild;
        bool isSelected;
        CancellationTokenSource? loadIconCts;
        ImageSource? icon;

        public string Path => path;
        public string Name => name;
        public int Level => level;
        public Thickness Margin => new Thickness(level * 16, 0, 0, 0);

        public bool IsExpanding
        {
            get;
            set => Set(ref field, value);
        }

        public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }
        public bool IsRenaming { get; set => Set(ref field, value); } = false;
        public string RenameText { get; set => Set(ref field, value); } = string.Empty;
        public System.Windows.Input.ICommand ClearCacheCommand { get; } = new ActionCommand(_ => true, _ => { });

        public event EventHandler? ExpandRequested;
        public event EventHandler? CollapseRequested;

        public ImageSource? Icon
        {
            get
            {
                if (icon != null) return icon;

                if (loadIconCts == null)
                {
                    var capturedPath = path;
                    loadIconCts = new CancellationTokenSource();
                    _ = LoadIconAsync(capturedPath, loadIconCts.Token);
                }

                return icon;
            }
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (Set(ref isExpanded, value))
                {
                    if (value) ExpandRequested?.Invoke(this, EventArgs.Empty);
                    else CollapseRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool HasDummyChild => hasDummyChild;

        public void SetHasDummyChild(bool value)
        {
            if (hasDummyChild == value) return;
            hasDummyChild = value;
            OnPropertyChanged(nameof(HasDummyChild));
        }

        public void ResetExpandedState()
        {
            isExpanded = false;
            OnPropertyChanged(nameof(IsExpanded));
        }

        public void MarkExpanded()
        {
            if (isExpanded) return;
            isExpanded = true;
            OnPropertyChanged(nameof(IsExpanded));
        }

        public ExplorerSidebarDirectoryViewModel(string path, string name, int level, bool hasChild)
        {
            this.path = path;
            this.name = name;
            this.level = level;
            this.hasDummyChild = hasChild;
        }

        public void UpdatePathAndName(string newPath)
        {
            path = newPath;
            name = new DirectoryInfo(newPath).Name;
            CancelLoad();
            icon = null;
            OnPropertyChanged(nameof(Path));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Icon));
        }

        async Task LoadIconAsync(string capturedPath, CancellationToken token)
        {
            var myCts = loadIconCts;
            try
            {
                var loadedIcon = await ShellImageLoader.LoadAsync(
                    () => ShellIcon.GetIcon(capturedPath, ShellIcon.GetIconSize(16), isDirectory: true), token);

                if (loadedIcon != null && !token.IsCancellationRequested)
                {
                    icon = loadedIcon;
                    OnPropertyChanged(nameof(Icon));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Default.Write("ExplorerSidebarDirectoryViewModel.Icon", e);
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

        public void SetImageSize(int iconSize, int thumbnailSize) { }

        public void CancelLoad()
        {
            var cts = loadIconCts;
            loadIconCts = null;
            cts?.Cancel();
            cts?.Dispose();
        }

        public void Dispose() => CancelLoad();
    }
}
