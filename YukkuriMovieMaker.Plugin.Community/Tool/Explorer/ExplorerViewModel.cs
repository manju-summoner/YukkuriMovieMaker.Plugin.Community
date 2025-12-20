using Microsoft.Xaml.Behaviors;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.Browser;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerViewModel : Bindable, IToolViewModel, IDisposable
    {
        readonly BoundedStack<string> undoHistory = new(100);
        readonly BoundedStack<string> redoHistory = new(100);

        public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

        public string Title
        {
            get
            {
                return GetTitle(Location);
            }
        }

        public string Location { get; private set => Set(ref field, value, nameof(Location), nameof(Title), nameof(IsFavorite)); } = "C:\\";

        public ActionCommand BackCommand { get; }
        public ActionCommand ForwardCommand { get; }
        public ActionCommand MoveToParentDirectoryCommand { get; }
        public ActionCommand RefreshCommand { get; }
        public ActionCommand NavigateCommand { get; }
        public ActionCommand CreateNewViewFromPathCommand { get; }
        public ActionCommand CreateNewViewCommand { get; }
        public ActionCommand IncreaseLayoutSizeCommand { get; }
        public ActionCommand DecreaseLayoutSizeCommand { get; }
        public ActionCommand CopyCommand { get; }
        public ActionCommand CutCommand { get; }
        public ActionCommand PasteCommand { get; }
        public ActionCommand DeleteCommand { get; }
        public ActionCommand OpenPropertiesCommand { get; }
        public ActionCommand OpenInWindowsExplorerCommand { get; }
        public ActionCommand OpenCurrentLocationInWindowsExplorerCommand { get; }
        public ActionCommand OpenWithDefaultAppCommand { get; }
        public ActionCommand DpiChangedCommand { get; }
        public ActionCommand SelectItemCommand { get; }
        public ActionCommand SelectAllCommand { get; }
        public ActionCommand UnselectAllCommand { get; }
        public ActionCommand SelectItemAfterCommand { get; }
        public ActionCommand OpenFavoriteEditorCommand { get; }
        public ActionCommand SwitchViewCommand { get; }

        public IEnumerable<ActionCommand> Commands => [
            BackCommand,
            ForwardCommand,
            MoveToParentDirectoryCommand,
            RefreshCommand,
            NavigateCommand,
            CreateNewViewFromPathCommand,
            CreateNewViewCommand,
            IncreaseLayoutSizeCommand,
            DecreaseLayoutSizeCommand,
            CopyCommand,
            CutCommand,
            PasteCommand,
            DeleteCommand,
            OpenPropertiesCommand,
            OpenInWindowsExplorerCommand,
            OpenCurrentLocationInWindowsExplorerCommand,
            OpenWithDefaultAppCommand,
            DpiChangedCommand,
            SelectItemCommand,
            SelectItemAfterCommand,
            SelectAllCommand,
            UnselectAllCommand,
            OpenFavoriteEditorCommand,
            SwitchViewCommand,
        ];


        public IExplorerItemViewModel[] Items { get; private set => Set(ref field, value); } = [];
        public IExplorerItemViewModel[] FilteredItems { get; private set => Set(ref field, value); } = [];
        public IExplorerItemViewModel[] SelectedItems { get; private set => Set(ref field, value); } = [];
        public string[] SelectedPaths { get; private set => Set(ref field, value); } = [];
        public IExplorerItemViewModel? ReadOnlyLastSelectedValue { get; set => Set(ref field, value); }

        public ExplorerFilter Filter { get; } = new ExplorerFilter();

        public ExplorerLayout Layout { get; private set => Set(ref field, value); } = new ExplorerLayout();

        readonly LeadingTrailingDebouncer<object> refreshDebouncer;
        FileSystemWatcher? watcher;

        DpiScale lastDpiScale = new(1, 1);

        public bool IsFavorite => ExplorerSettings.Default.Favorites.Any(x => x.Url == Location);
        public ExplorerFavoriteEditorViewModel? FavoriteEditorViewModel { get; set => Set(ref field, value); }

        [SuppressMessage("Performance", "CA1822:メンバーを static に設定します", Justification = "")]
        public ExplorerFavoriteDirectoryViewModel FavoriteDirectoryViewModel => ExplorerFavoriteDirectoryViewModel.CreateExplorerFavoriteRoot();

        public ExplorerViewModel()
        {
            BackCommand = new ActionCommand(
                _ => undoHistory.Count > 0,
                _ =>
                {
                    if (undoHistory.Count == 0)
                        return;
                    redoHistory.Push(Location);
                    Location = undoHistory.Pop();
                    RequestRefresh();
                });
            ForwardCommand = new ActionCommand(
                _ => redoHistory.Count > 0,
                _ =>
                {
                    if (redoHistory.Count == 0)
                        return;
                    undoHistory.Push(Location);
                    Location = redoHistory.Pop();
                    RequestRefresh();
                });
            MoveToParentDirectoryCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(Location) && Path.GetDirectoryName(Location) is not null,
                _ =>
                {
                    var parent = Path.GetDirectoryName(Location);
                    if (parent is null)
                        return;
                    undoHistory.Push(Location);
                    redoHistory.Clear();
                    Location = parent;
                    RequestRefresh();
                });
            RefreshCommand = new ActionCommand(
                _ => true,
                _ => RequestRefresh());
            NavigateCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(Location) && Directory.Exists(Location),
                x =>
                {
                    var location = x as string;
                    if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
                        return;
                    undoHistory.Push(Location);
                    redoHistory.Clear();
                    Location = location;
                    RequestRefresh();
                });
            CreateNewViewFromPathCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if(x is not string path || string.IsNullOrEmpty(path))
                        return;
                    var toolState = new ToolState()
                    {
                        SavedState = Json.Json.GetJsonText(new ExplorerState(path, Layout.Clone(), new ExplorerFilter()))
                    };
                    var args = new CreateNewToolViewRequestedEventArgs(toolState);
                    CreateNewToolViewRequested?.Invoke(this, args);
                });
            CreateNewViewCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var paths= FilteredItems.OfType<ExplorerDirectoryItemViewModel>().Select(item => item.Path).ToArray();
                    foreach(var path in paths)
                    {
                        var toolState = new ToolState()
                        {
                            SavedState = Json.Json.GetJsonText(new ExplorerState(path, Layout.Clone(), new ExplorerFilter()))
                        };
                        var args = new CreateNewToolViewRequestedEventArgs(toolState);
                        CreateNewToolViewRequested?.Invoke(this, args);
                    }
                });
            IncreaseLayoutSizeCommand = new ActionCommand(
                _ => Layout.CanIncreaseLayoutSize(),
                _ =>
                {
                    Layout.IncreaseLayoutSize();
                    UpdateImageSize();
                    RaiseCommandExecutable();
                });
            DecreaseLayoutSizeCommand = new ActionCommand(
                _ => Layout.CanDecreaseLayoutSize(),
                _ =>
                {
                    Layout.DecreaseLayoutSize();
                    UpdateImageSize();
                    RaiseCommandExecutable();
                });

            CopyCommand = new ActionCommand(
                _ => SelectedPaths.Length > 0,
                _ => ShellClipboard.CopyFiles(SelectedPaths));
            CutCommand = new ActionCommand(
                _ => SelectedPaths.Length > 0,
                _ => ShellClipboard.CutFiles(SelectedPaths));
            PasteCommand = new ActionCommand(
                _ => true,
                _ => 
                {
                    var data = Clipboard.GetDataObject();
                    if (data is null)
                        return;
                    var formats = data.GetFormats();
                    if (formats.Contains(DataFormats.FileDrop))
                    {
                        if (data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
                            return;
                        var effect = ShellClipboard.GetPreferredDropEffect(data) ?? DragDropEffects.Copy;
                        // LocationがDependencyPropertyでUIスレッドからしか取得できないため、ローカル変数にコピーしておく
                        var location = Location;
                        var thread = new Thread(() =>
                        {
                            try
                            {
                                if (effect.HasFlag(DragDropEffects.Move))
                                    ShellFileOperation.MoveFiles(paths, location);
                                else
                                    ShellFileOperation.CopyFiles(paths, location);
                            }
                            catch (Exception e)
                            {
                                Log.Default.Write("ファイルの貼り付け処理中に例外が発生しました。", e);
                            }
                        });
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                    }
                    else
                    {
                        var handler = DataObjectFormatHandler.FromDataObject(data);
                        if (handler.HasFiles())
                        {
                            // リソースフォルダに展開されたデータ形式のファイルをすべてLocationに移動させる
                            var paths =
                                handler
                                .GetFiles()
                                .Where(file=>ShellFileOperation.CanMove(file, Location))
                                .ToArray();
                            // LocationがDependencyPropertyでUIスレッドからしか取得できないため、ローカル変数にコピーしておく
                            var location = Location;
                            var thread = new Thread(() =>
                            {
                                try
                                {
                                    ShellFileOperation.MoveFiles(paths, location);
                                }
                                catch (Exception e)
                                {
                                    Log.Default.Write("ファイルの貼り付け処理中に例外が発生しました。", e);
                                }
                            });
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.Start();
                        }
                    }
                });
            DeleteCommand = new ActionCommand(
                _ => SelectedPaths.Length > 0,
                _ =>
                {
                    // SelectedPaths内部でUIスレッドでしか触れないDependencyPropertyにアクセスしているため、ローカル変数にコピーしておく
                    var selectedPaths = SelectedPaths;
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            ShellFileOperation.DeleteFiles(selectedPaths);
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write("ファイルの削除処理中に例外が発生しました。", e);
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                });

            OpenPropertiesCommand = new ActionCommand(
                _ => SelectedPaths.Length > 0,
                _ =>
                {
                    foreach (var path in SelectedPaths)
                    {
                        if(!File.Exists(path) && !Directory.Exists(path))
                            continue;
                        PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, null);
                    }
                });

            OpenInWindowsExplorerCommand = new ActionCommand(
                _ => SelectedItems.OfType<ExplorerDirectoryItemViewModel>().Any(),
                _ =>
                {
                    foreach(var path in SelectedItems.OfType<ExplorerDirectoryItemViewModel>().Select(item=>item.Path))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", path);
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write($"エクスプローラーの起動に失敗しました。path={path}", e);
                        }
                    }
                });
            OpenCurrentLocationInWindowsExplorerCommand = new ActionCommand(
                _ => Directory.Exists(Location),
                _ =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", Location);
                    }
                    catch (Exception e)
                    {
                        Log.Default.Write($"エクスプローラーの起動に失敗しました。path={Location}", e);
                    }
                });
            OpenWithDefaultAppCommand = new ActionCommand(
                _ => SelectedPaths.Length > 0,
                _ =>
                {
                    foreach (var path in SelectedPaths)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                            {
                                FileName = path,
                                UseShellExecute = true,
                            });
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write($"ファイルの関連付けられたアプリケーションでの起動に失敗しました。path={path}", e);
                        }
                    }
                });
            DpiChangedCommand = new ActionCommand(
                _ => true,
                x => 
                {
                    if(x is DpiScale args)
                        lastDpiScale = args;
                    UpdateImageSize();
                });
            bool isSelected = false;
            SelectItemCommand = new ActionCommand(
                _=>true,
                x => 
                {
                    if(x is not MouseEventArgs args)
                        return;
                    BeginSelectionChange();
                    isSelected = true;
                    try
                    {
                        if(args.Source is not FrameworkElement element || element.DataContext is not IExplorerItemViewModel vm)
                            return;
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            vm.IsSelected = !vm.IsSelected;
                        }
                        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        {
                            var selectedItem = ReadOnlyLastSelectedValue;
                            if (selectedItem is null)
                            {
                                foreach (var item in Items)
                                {
                                    item.IsSelected = item == vm;
                                }
                                return;
                            }

                            var startIndex = Math.Min(Items.IndexOf(selectedItem), Items.IndexOf(vm));
                            var endIndex = Math.Max(Items.IndexOf(selectedItem), Items.IndexOf(vm));
                            for (int i = 0; i < Items.Length; i++)
                            {
                                Items[i].IsSelected = (startIndex <= i && i <= endIndex);
                            }
                        }
                        else
                        {
                            foreach (var item in Items)
                            {
                                item.IsSelected = item == vm;
                            }
                        }
                    }
                    finally
                    {
                        EndSelectionChange();
                        args.Handled = true;
                    }
                });
            SelectItemAfterCommand = new ActionCommand(
                _ => Items.Length > 0,
                x =>
                {
                    if(isSelected)
                    {
                        isSelected = false;
                        return;
                    }
                    isSelected = false;
                    if (x is not IExplorerItemViewModel vm)
                        return;
                    BeginSelectionChange();
                    try
                    {
                        foreach (var item in Items)
                        {
                            item.IsSelected = item == vm;
                        }
                    }
                    finally
                    {
                        EndSelectionChange();
                    }
                });
            SelectAllCommand = new ActionCommand(
                _ => Items.Length > 0,
                _ =>
                {
                    BeginSelectionChange();
                    foreach (var item in Items)
                    {
                        item.IsSelected = true;
                    }
                    EndSelectionChange();
                });
            UnselectAllCommand = new ActionCommand(
                _ => SelectedItems.Length > 0,
                _ =>
                {
                    BeginSelectionChange();
                    foreach (var item in Items)
                    {
                        item.IsSelected = false;
                    }
                    EndSelectionChange();
                });
            OpenFavoriteEditorCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is not string location)
                        return;
                    var favorite = ExplorerSettings.Default.Favorites.FirstOrDefault(x => x.Url == location);
                    if (favorite is null)
                    {
                        favorite = new ExplorerFavorite()
                        {
                            Url = location,
                            Name = GetTitle(location),
                            Directory = string.Empty,
                        };
                        ExplorerSettings.Default.Favorites.Add(favorite);
                    }
                    FavoriteEditorViewModel = new ExplorerFavoriteEditorViewModel(favorite);
                    FavoriteEditorViewModel = null;
                    OnPropertyChanged(nameof(IsFavorite));
                    OnPropertyChanged(nameof(FavoriteDirectoryViewModel));
                });
            SwitchViewCommand = new ActionCommand(
                _ => true,
                x => 
                {
                    if (x is not ExplorerViewMode mode)
                        return;
                    var spec = ExplorerLayoutSpec.All.FirstOrDefault(x => x.Mode == mode);
                    if (spec is null)
                        return;
                    Layout.Mode = spec.Mode;
                    Layout.IconSize = spec.MinimumIconSize;
                });


            refreshDebouncer = new(args =>
            {
                if (Application.Current.Dispatcher.CheckAccess())
                    Refresh();
                else
                    Application.Current.Dispatcher.Invoke(() => Refresh());
            });

            this.PropertyChanged += ExplorerViewModel_PropertyChanged;
            this.Filter.FilterChanged += Filter_FilterChanged;

            RequestRefresh();
        }

        private void Filter_FilterChanged(object? sender, EventArgs e)
        {
            UpdateFilteredItems();
        }

        private void UpdateImageSize()
        {
            var dpiScale = Math.Max(lastDpiScale.DpiScaleX, lastDpiScale.DpiScaleY);
            foreach (var item in Items)
            {
                item.SetImageSize((int)(Layout.IconSize * dpiScale), (int)(300 * dpiScale));
            }
        }

        private void ExplorerViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Items))
                UpdateFilteredItems();
            else if (e.PropertyName is nameof(FilteredItems))
                UpdateSelectedItems();
            else if (e.PropertyName is nameof(SelectedItems))
                UpdateSelectedPaths();
        }

        private void UpdateFilteredItems()
        {
            if (!Filter.IsFiltered)
            {
                FilteredItems = Items;
                return;
            }
            var filteredItems = Items.Where(item => Filter.IsMatch(item.Path));
            FilteredItems = [.. filteredItems];
        }

        bool isSkipUpdateSelection = false;
        void BeginSelectionChange()
        {
            isSkipUpdateSelection = true;
        }
        void EndSelectionChange()
        {
            isSkipUpdateSelection = false;
            UpdateSelectedItems();
            UpdateSelectedPaths();
            RaiseCommandExecutable();
        }

        private void UpdateSelectedItems()
        {
            if (isSkipUpdateSelection)
                return;
            var selectedItems =
                FilteredItems
                .Where(item => item.IsSelected);
            SelectedItems = [.. selectedItems];
        }

        private void UpdateSelectedPaths()
        {
            if (isSkipUpdateSelection)
                return;
            SelectedPaths = [.. SelectedItems.Select(item => item.Path)];
        }

        void RequestRefresh() => refreshDebouncer.Signal(null);
        
        void StopFileSystemWatcher()
        {
            watcher?.Created -= Watcher_Callback;
            watcher?.Deleted -= Watcher_Callback;
            watcher?.Renamed -= Watcher_Callback;
            watcher?.EnableRaisingEvents = false;
            watcher?.Dispose();
            watcher = null;
        }

        void Refresh()
        {
            StopFileSystemWatcher();
            foreach (var item in Items)
                item.PropertyChanged -= ItemViewModel_PropertyChanged;
            Items = [];

            if (!Directory.Exists(Location))
            {
                RaiseCommandExecutable();
                return;
            }

            try
            {
                var options = new EnumerationOptions()
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                };
                var di = new DirectoryInfo(Location);
                var dirs = di.GetDirectories("*", options).Select(dir => new ExplorerDirectoryItemViewModel(dir.FullName)).Cast<IExplorerItemViewModel>();
                var files = di.GetFiles("*", options).Select(file => new ExplorerFileItemViewModel(file.FullName)).Cast<IExplorerItemViewModel>();

                Items = [.. dirs, .. files];
                foreach(var item in Items)
                    item.PropertyChanged += ItemViewModel_PropertyChanged;
                UpdateImageSize();
            }
            catch
            {
                Items = [];
            }

            watcher = new FileSystemWatcher(Location);
            watcher.Created += Watcher_Callback;
            watcher.Deleted += Watcher_Callback;
            watcher.Renamed += Watcher_Callback;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
            try
            {
                watcher.EnableRaisingEvents = true;
            }
            catch
            {

            }
            RaiseCommandExecutable();
        }

        private void Watcher_Callback(object sender, FileSystemEventArgs e)
        {
            if(e.ChangeType
                is WatcherChangeTypes.Created
                or WatcherChangeTypes.Deleted
                or WatcherChangeTypes.Renamed)
            {
                refreshDebouncer.Signal(null);
            }
        }

        private void ItemViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IExplorerItemViewModel.IsSelected))
            {
                UpdateSelectedItems();
                RaiseCommandExecutable();
            }
        }

        void RaiseCommandExecutable()
        {
            foreach (var command in Commands)
                command.RaiseCanExecuteChanged();
        }


        public void LoadState(ToolState stateData)
        {
            var savedState = stateData.SavedState;
            if(string.IsNullOrEmpty(savedState))
                return;
            var state = Json.Json.LoadFromText<ExplorerState>(savedState);
            if (state is null)
                return;
            Location = state.Location;
            Layout = state.Layout;
            Filter.CopyFrom(state.Filter);
            Refresh();
        }

        public ToolState SaveState()
        {
            return new ToolState() 
            {
                SavedState = Json.Json.GetJsonText(new ExplorerState(Location, Layout.Clone(), Filter))
            };
        }

        static string GetTitle(string location)
        {
            if (string.IsNullOrEmpty(location))
                return Texts.Explorer;
            var name = Path.GetFileName(location.TrimEnd(Path.DirectorySeparatorChar));
            return string.IsNullOrEmpty(name) ? location : name;
        }

        #region IDisposable Support

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // マネージド状態を破棄します (マネージド オブジェクト)
                }
                StopFileSystemWatcher();

                // アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~ExplorerViewModel()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
