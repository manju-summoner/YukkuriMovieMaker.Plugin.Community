using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class ExplorerViewModel : Bindable, IToolViewModel, IDisposable
    {
        readonly BoundedStack<string> undoHistory = new(100);
        readonly BoundedStack<string> redoHistory = new(100);

        bool isSidebarLastActive = false;

        public event EventHandler<CreateNewToolViewRequestedEventArgs>? CreateNewToolViewRequested;

        public string Title => GetTitle(Location);

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
        public ActionCommand SelectAllCommand { get; }
        public ActionCommand UnselectAllCommand { get; }
        public ActionCommand OpenFavoriteEditorCommand { get; }
        public ActionCommand SwitchViewCommand { get; }
        public ActionCommand SwitchSortKeyCommand { get; }
        public ActionCommand SwitchSortOrderCommand { get; }
        public ActionCommand OpenFileItemCommand { get; }
        public ActionCommand CreateNewFolderCommand { get; }
        public ActionCommand RenameCommand { get; }
        public ActionCommand CommitRenameCommand { get; }
        public ActionCommand CancelRenameCommand { get; }
        public ActionCommand CompressCommand { get; }
        public ActionCommand ExtractCommand { get; }
        public ActionCommand SevenZipCompressCommand { get; }
        public ActionCommand SevenZipExtractCommand { get; }

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
            SelectAllCommand,
            UnselectAllCommand,
            OpenFavoriteEditorCommand,
            SwitchViewCommand,
            SwitchSortKeyCommand,
            SwitchSortOrderCommand,
            OpenFileItemCommand,
            CreateNewFolderCommand,
            RenameCommand,
            CommitRenameCommand,
            CancelRenameCommand,
            CompressCommand,
            ExtractCommand,
            SevenZipCompressCommand,
            SevenZipExtractCommand,
        ];

        public IExplorerItemViewModel[] Items { get; private set => Set(ref field, value); } = [];
        public IExplorerItemViewModel[] FilteredItems { get; private set => Set(ref field, value); } = [];
        public IExplorerSelectableItem[] SelectedItems { get; private set => Set(ref field, value); } = [];
        public string[] SelectedPaths { get; private set => Set(ref field, value); } = [];

        public ExplorerFilter Filter { get; } = new ExplorerFilter();

        public ExplorerLayout Layout
        {
            get => field;
            private set
            {
                if (field != null) field.PropertyChanged -= Layout_PropertyChanged;
                Set(ref field!, value);
                if (field != null) field.PropertyChanged += Layout_PropertyChanged;
                NotifyLayoutModeProperties();
            }
        } = new ExplorerLayout();

        public ExplorerSortKey SortKey { get => field; set => Set(ref field, value, nameof(SortKey), nameof(IsSortByName), nameof(IsSortByLastWriteTime), nameof(IsSortByExtension)); } = ExplorerSortKey.Name;
        public ExplorerSortOrder SortOrder { get => field; set => Set(ref field, value, nameof(SortOrder), nameof(IsSortAscending), nameof(IsSortDescending)); } = ExplorerSortOrder.Ascending;

        public bool IsSortByName => SortKey == ExplorerSortKey.Name;
        public bool IsSortByLastWriteTime => SortKey == ExplorerSortKey.LastWriteTime;
        public bool IsSortByExtension => SortKey == ExplorerSortKey.Extension;
        public bool IsSortAscending => SortOrder == ExplorerSortOrder.Ascending;
        public bool IsSortDescending => SortOrder == ExplorerSortOrder.Descending;

        public bool IsViewList => Layout.Mode == ExplorerViewMode.List;
        public bool IsViewWrapList => Layout.Mode == ExplorerViewMode.WrapList;
        public bool IsViewTiles => Layout.Mode == ExplorerViewMode.Tiles;

        public bool IsSevenZipAvailable => SevenZipService.IsAvailable;

        readonly LeadingTrailingDebouncer<object> refreshDebouncer;
        FileSystemWatcher? watcher;
        CancellationTokenSource? refreshCts;
        CancellationTokenSource? sidebarSyncCts;
        readonly CancellationTokenSource disposeCts = new();
        volatile bool isLoading = false;
        string? pendingRenamePath;

        DpiScale lastDpiScale = new(1, 1);

        public bool IsFavorite => ExplorerSettings.Default.Favorites.Any(x => x.Url == Location);
        public ExplorerFavoriteEditorViewModel? FavoriteEditorViewModel { get; set => Set(ref field, value); }
        [SuppressMessage("Performance", "CA1822:メンバーを static に設定します", Justification = "")]
        public ExplorerFavoriteDirectoryViewModel FavoriteDirectoryViewModel => ExplorerFavoriteDirectoryViewModel.CreateExplorerFavoriteRoot();

        public ObservableCollection<AddressBarSuggestion> FavoriteUrls { get; } = [];

        public ObservableCollection<ExplorerSidebarDirectoryViewModel> SidebarItems { get; } = [];

        public System.Windows.Input.ICommand DropCompletedCommand { get; }

        public ExplorerViewModel()
        {
            Layout = new ExplorerLayout();
            CollectionChangedEventManager.AddHandler(ExplorerSettings.Default.Favorites, Favorites_CollectionChanged);
            foreach (var favorite in ExplorerSettings.Default.Favorites)
            {
                PropertyChangedEventManager.AddHandler(favorite, Favorite_PropertyChanged, string.Empty);
            }

            BackCommand = new ActionCommand(
                _ => !isLoading && undoHistory.Count > 0,
                _ =>
                {
                    CommitAnyPendingRename();
                    if (undoHistory.Count == 0) return;
                    redoHistory.Push(Location);
                    Location = undoHistory.Pop();
                    RequestRefresh();
                });
            ForwardCommand = new ActionCommand(
                _ => !isLoading && redoHistory.Count > 0,
                _ =>
                {
                    CommitAnyPendingRename();
                    if (redoHistory.Count == 0) return;
                    undoHistory.Push(Location);
                    Location = redoHistory.Pop();
                    RequestRefresh();
                });
            MoveToParentDirectoryCommand = new ActionCommand(
                _ => !isLoading && !string.IsNullOrEmpty(Location) && Path.GetDirectoryName(Location) is not null,
                _ =>
                {
                    CommitAnyPendingRename();
                    var parent = Path.GetDirectoryName(Location);
                    if (parent is null) return;
                    undoHistory.Push(Location);
                    redoHistory.Clear();
                    Location = parent;
                    RequestRefresh();
                });
            RefreshCommand = new ActionCommand(_ => !isLoading, _ =>
            {
                CommitAnyPendingRename();
                RequestRefresh();
            });
            NavigateCommand = new ActionCommand(
                _ => !isLoading,
                x =>
                {
                    CommitAnyPendingRename();
                    var raw = x as string;
                    var location = ResolveNavigationTarget(raw);
                    if (string.IsNullOrEmpty(location) || !TryCheckFileSystemAccess(location, true)) return;

                    undoHistory.Push(Location);
                    redoHistory.Clear();
                    Location = location;
                    RequestRefresh();
                });
            CreateNewViewFromPathCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is not string path || string.IsNullOrEmpty(path)) return;
                    var toolState = new ToolState()
                    {
                        SavedState = Json.Json.GetJsonText(new ExplorerState(path, Layout.Clone(), new ExplorerFilter()) { SortKey = SortKey, SortOrder = SortOrder })
                    };
                    CreateNewToolViewRequested?.Invoke(this, new CreateNewToolViewRequestedEventArgs(toolState));
                });
            CreateNewViewCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var paths = FilteredItems.OfType<ExplorerDirectoryItemViewModel>().Select(item => item.Path).ToArray();
                    foreach (var path in paths)
                    {
                        var toolState = new ToolState()
                        {
                            SavedState = Json.Json.GetJsonText(new ExplorerState(path, Layout.Clone(), new ExplorerFilter()) { SortKey = SortKey, SortOrder = SortOrder })
                        };
                        CreateNewToolViewRequested?.Invoke(this, new CreateNewToolViewRequestedEventArgs(toolState));
                    }
                });
            IncreaseLayoutSizeCommand = new ActionCommand(
                _ => Layout.CanIncreaseLayoutSize(),
                _ =>
                {
                    Layout.IncreaseLayoutSize();
                    RaiseCommandExecutable();
                });
            DecreaseLayoutSizeCommand = new ActionCommand(
                _ => Layout.CanDecreaseLayoutSize(),
                _ =>
                {
                    Layout.DecreaseLayoutSize();
                    RaiseCommandExecutable();
                });

            CopyCommand = new ActionCommand(p => GetActiveSelectedPaths(p).Length > 0, p => ShellClipboard.CopyFiles(GetActiveSelectedPaths(p)));
            CutCommand = new ActionCommand(p => GetActiveSelectedPaths(p).Length > 0, p => ShellClipboard.CutFiles(GetActiveSelectedPaths(p)));
            PasteCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    var data = Clipboard.GetDataObject();
                    if (data is null) return;
                    var formats = data.GetFormats();
                    if (formats.Contains(DataFormats.FileDrop))
                    {
                        if (data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
                        var effect = ShellClipboard.GetPreferredDropEffect(data) ?? DragDropEffects.Copy;
                        var location = Location;
                        RunOnStaThread(() =>
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
                    }
                    else
                    {
                        var handler = DataObjectFormatHandler.FromDataObject(data);
                        if (handler.HasFiles())
                        {
                            var paths = handler.GetFiles().Where(file => ShellFileOperation.CanMove(file, Location)).ToArray();
                            var location = Location;
                            RunOnStaThread(() =>
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
                        }
                    }
                });
            DeleteCommand = new ActionCommand(
                p => GetActiveSelectedPaths(p).Length > 0,
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    RunOnStaThread(() =>
                    {
                        try
                        {
                            ShellFileOperation.DeleteFiles(paths);
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write("ファイルの削除処理中に例外が発生しました。", e);
                        }
                    });
                });

            OpenPropertiesCommand = new ActionCommand(
                p => GetActiveSelectedPaths(p).Length == 1,
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    foreach (var path in paths)
                    {
                        if (!TryCheckFileSystemAccess(path, null)) continue;
                        try
                        {
                            PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, null);
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write($"プロパティの表示中に例外が発生しました。path={path}", e);
                        }
                    }
                });

            OpenInWindowsExplorerCommand = new ActionCommand(
                p => GetActiveSelectedItems(p).Any(item => item is ExplorerDirectoryItemViewModel || item is ExplorerSidebarDirectoryViewModel),
                p =>
                {
                    var paths = GetActiveSelectedItems(p)
                        .Where(item => item is ExplorerDirectoryItemViewModel || item is ExplorerSidebarDirectoryViewModel)
                        .Select(item => item.Path);
                    foreach (var path in paths)
                        StartProcessSafe("explorer.exe", path);
                });
            OpenCurrentLocationInWindowsExplorerCommand = new ActionCommand(
                _ => TryCheckFileSystemAccess(Location, true),
                _ => StartProcessSafe("explorer.exe", Location));
            OpenWithDefaultAppCommand = new ActionCommand(
                p => GetActiveSelectedPaths(p).Length > 0,
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    foreach (var path in paths)
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
                    if (x is DpiScale args) lastDpiScale = args;
                    UpdateItemsImageSize();
                });


            SelectAllCommand = new ActionCommand(
                _ => Items.Length > 0,
                _ =>
                {
                    CommitAnyPendingRename();
                    BeginSelectionChange();
                    foreach (var item in Items) item.IsSelected = true;
                    EndSelectionChange();
                });
            UnselectAllCommand = new ActionCommand(
                _ => SelectedItems.Length > 0 || Items.Any(x => x.IsRenaming) || SidebarItems.Any(x => x.IsRenaming),
                _ =>
                {
                    CommitAnyPendingRename();
                    BeginSelectionChange();
                    foreach (var item in Items) item.IsSelected = false;
                    foreach (var item in SidebarItems) item.IsSelected = false;
                    EndSelectionChange();
                });
            OpenFavoriteEditorCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is not string location) return;
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
                    if (x is not ExplorerViewMode mode) return;
                    var spec = ExplorerLayoutSpec.All.FirstOrDefault(x => x.Mode == mode);
                    if (spec is null) return;
                    Layout.Mode = spec.Mode;
                    Layout.IconSize = spec.MinimumIconSize;
                });
            SwitchSortKeyCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is ExplorerSortKey key)
                    {
                        SortKey = key;
                        UpdateFilteredItems();
                    }
                    else if (x is string s && Enum.TryParse<ExplorerSortKey>(s, out var parsed))
                    {
                        SortKey = parsed;
                        UpdateFilteredItems();
                    }
                });
            SwitchSortOrderCommand = new ActionCommand(
                _ => true,
                x =>
                {
                    if (x is ExplorerSortOrder order)
                    {
                        SortOrder = order;
                        UpdateFilteredItems();
                    }
                    else if (x is string s && Enum.TryParse<ExplorerSortOrder>(s, out var parsed))
                    {
                        SortOrder = parsed;
                        UpdateFilteredItems();
                    }
                });
            OpenFileItemCommand = new ActionCommand(
                _ => !isLoading,
                x =>
                {
                    CommitAnyPendingRename();
                    var raw = x as string;
                    var resolved = ResolveNavigationTarget(raw);
                    if (string.IsNullOrEmpty(resolved)) return;

                    if (TryCheckFileSystemAccess(resolved, true))
                    {
                        undoHistory.Push(Location);
                        redoHistory.Clear();
                        Location = resolved;
                        RequestRefresh();
                        return;
                    }

                    ICommand? addFileCommand = Settings.CommandSettings.Default[Settings.CommandType.AddFileItem];
                    var paths = new[] { resolved };
                    if (addFileCommand?.CanExecute(paths) == true)
                        addFileCommand.Execute(paths);
                });
            CreateNewFolderCommand = new ActionCommand(
                p =>
                {
                    var target = p is ExplorerSidebarDirectoryViewModel sidebar ? sidebar.Path : Location;
                    return TryCheckFileSystemAccess(target, true);
                },
                p =>
                {
                    var target = p is ExplorerSidebarDirectoryViewModel sidebar ? sidebar.Path : Location;
                    if (!TryCheckFileSystemAccess(target, true)) return;

                    CommitAnyPendingRename();

                    _ = Task.Run(() =>
                    {
                        try
                        {
                            var name = Texts.NewFolderName;
                            var path = Path.Combine(target, name);
                            int index = 1;
                            while (Directory.Exists(path) || File.Exists(path))
                            {
                                path = Path.Combine(target, $"{name} ({index})");
                                index++;
                            }
                            Directory.CreateDirectory(path);

                            _ = Application.Current.Dispatcher.InvokeAsync(
                                () => AfterFolderCreatedAsync(p, path),
                                System.Windows.Threading.DispatcherPriority.Input);
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write("新規フォルダ作成中に例外が発生しました。", e);
                        }
                    });
                });
            CompressCommand = new ActionCommand(
                p => GetActiveSelectedPaths(p).Length > 0,
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    if (paths.Length == 0) return;
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            var destination = Path.GetDirectoryName(paths[0]) ?? Location;
                            var archiveName = BuildArchiveName(paths) + ".zip";
                            var archivePath = Path.Combine(destination, archiveName);
                            int index = 1;
                            while (File.Exists(archivePath) || Directory.Exists(archivePath))
                            {
                                archivePath = Path.Combine(destination, $"{Path.GetFileNameWithoutExtension(archiveName)} ({index}).zip");
                                index++;
                            }
                            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
                            foreach (var path in paths)
                            {
                                if (Directory.Exists(path))
                                    AddDirectoryToArchive(archive, path, Path.GetFileName(path));
                                else if (File.Exists(path))
                                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
                            }
                            
                            Application.Current.Dispatcher.Invoke(() => RequestRefresh());
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write("圧縮処理中に例外が発生しました。", e);
                        }
                    });
                });
            ExtractCommand = new ActionCommand(
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    return paths.Length > 0 && paths.All(path =>
                        string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase));
                },
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    _ = Task.Run(() =>
                    {
                        foreach (var archivePath in paths)
                        {
                            try
                            {
                                var destination = Path.Combine(
                                    Path.GetDirectoryName(archivePath) ?? Location,
                                    Path.GetFileNameWithoutExtension(archivePath));
                                ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: false);
                            }
                            catch (Exception e)
                            {
                                Log.Default.Write($"解凍処理中に例外が発生しました。path={archivePath}", e);
                            }
                        }
                    });
                });
            SevenZipCompressCommand = new ActionCommand(
                p => SevenZipService.IsAvailable && GetActiveSelectedPaths(p).Length > 0,
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    if (paths.Length == 0) return;
                    var destination = Path.GetDirectoryName(paths[0]) ?? Location;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SevenZipService.CompressAsync(paths, destination, disposeCts.Token);
                            Application.Current.Dispatcher.Invoke(() => RequestRefresh());
                        }
                        catch (Exception e)
                        {
                            Log.Default.Write("7-Zip 圧縮処理中に例外が発生しました。", e);
                        }
                    });
                });
            SevenZipExtractCommand = new ActionCommand(
                p => SevenZipService.IsAvailable && GetActiveSelectedPaths(p).Length > 0,
                p =>
                {
                    var paths = GetActiveSelectedPaths(p);
                    _ = Task.Run(async () =>
                    {
                        var anySucceeded = false;
                        foreach (var archivePath in paths)
                        {
                            try
                            {
                                var destination = Path.Combine(
                                    Path.GetDirectoryName(archivePath) ?? Location,
                                    Path.GetFileNameWithoutExtension(archivePath));
                                await SevenZipService.ExtractAsync(archivePath, destination, disposeCts.Token);
                                anySucceeded = true;
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception e)
                            {
                                Log.Default.Write($"7-Zip 解凍処理中に例外が発生しました。path={archivePath}", e);
                            }
                        }
                        if (anySucceeded)
                            Application.Current.Dispatcher.Invoke(() => RequestRefresh());
                    });
                });

            DropCompletedCommand = new ActionCommand(
                _ => true,
                p =>
                {
                    if (p is string path && !string.IsNullOrEmpty(path))
                    {
                        if (string.Equals(Location.TrimEnd(Path.DirectorySeparatorChar), path.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        {
                            RequestRefresh();
                        }
                        else
                        {
                            var targetNode = SidebarItems.FirstOrDefault(x => string.Equals(x.Path.TrimEnd(Path.DirectorySeparatorChar), path.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
                            if (targetNode != null)
                            {
                                var capturedPath = targetNode.Path;
                                var capturedIsExpanded = targetNode.IsExpanded;
                                var capturedHasDummyChild = targetNode.HasDummyChild;
                                _ = Task.Run(() =>
                                {
                                    try
                                    {
                                        var options = new EnumerationOptions()
                                        {
                                            IgnoreInaccessible = true,
                                            RecurseSubdirectories = false,
                                            ReturnSpecialDirectories = false,
                                            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                                        };
                                        var di = new DirectoryInfo(capturedPath);

                                        if (capturedIsExpanded)
                                        {
                                            var list = new List<(DirectoryInfo dir, bool hasChild)>();
                                            foreach (var dir in di.EnumerateDirectories("*", options).OrderBy(d => d.Name))
                                            {
                                                bool hasChild = false;
                                                try { hasChild = dir.EnumerateDirectories("*", options).Any(); } catch { }
                                                list.Add((dir, hasChild));
                                            }

                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                SyncSidebarChildren(targetNode, list);
                                            });
                                        }
                                        else
                                        {
                                            bool hasChild = false;
                                            try { hasChild = di.EnumerateDirectories("*", options).Any(); } catch { }
                                            if (hasChild != capturedHasDummyChild)
                                            {
                                                Application.Current.Dispatcher.InvokeAsync(() => targetNode.SetHasDummyChild(hasChild));
                                            }
                                        }
                                    }
                                    catch { }
                                });
                            }
                        }
                    }
                });

            RenameCommand = new ActionCommand(
                p => GetActiveItem(p) != null,
                p =>
                {
                    var item = GetActiveItem(p);
                    if (item == null) return;
                    CommitAnyPendingRename();
                    item.RenameText = item.Name;
                    item.IsRenaming = true;
                });

            CommitRenameCommand = new ActionCommand(
                p => p is IExplorerSelectableItem item && item.IsRenaming,
                p =>
                {
                    if (p is not IExplorerSelectableItem item) return;
                    ExecuteRename(item);
                });

            CancelRenameCommand = new ActionCommand(
                _ => true,
                p =>
                {
                    if (p is not IExplorerSelectableItem item) return;
                    item.IsRenaming = false;
                });

            refreshDebouncer = new(_ => Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshInternalAsync()));

            this.PropertyChanged += ExplorerViewModel_PropertyChanged;
            this.Filter.FilterChanged += Filter_FilterChanged;

            RequestRefresh();
            RefreshFavoriteUrls();
            _ = InitializeDrivesAsync();
        }

        void CommitAnyPendingRename()
        {
            var renamingItems = Items.Where(x => x.IsRenaming)
                                     .Concat<IExplorerSelectableItem>(SidebarItems.Where(x => x.IsRenaming))
                                     .ToList();
            foreach (var item in renamingItems)
                ExecuteRename(item);
        }

        void ExecuteRename(IExplorerSelectableItem item)
        {
            if (!item.IsRenaming) return;

            var oldName = item.Name;
            var newName = item.RenameText?.Trim();

            item.IsRenaming = false;

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                return;

            var oldPath = item.Path.TrimEnd(Path.DirectorySeparatorChar);
            var parent = Path.GetDirectoryName(oldPath);
            if (parent == null) return;

            var newPath = Path.Combine(parent, newName);
            if (string.Equals(oldPath, newPath, StringComparison.Ordinal)) return;

            _ = Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(oldPath))
                        Directory.Move(oldPath, newPath);
                    else if (File.Exists(oldPath))
                        File.Move(oldPath, newPath);

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        UpdateItemPaths(oldPath, newPath);
                    });
                }
                catch (Exception e)
                {
                    Log.Default.Write($"名前変更中に例外が発生しました。path={oldPath}", e);
                }
            });
        }

        async Task AfterFolderCreatedAsync(object? p, string path)
        {
            if (p is ExplorerSidebarDirectoryViewModel sidebarVm)
            {
                if (sidebarVm.IsExpanded)
                {
                    CollapseSidebarItem(sidebarVm);
                    sidebarVm.ResetExpandedState();
                }
                await ExpandSidebarItemAsync(sidebarVm);
                sidebarVm.IsExpanded = true;

                var newItem = SidebarItems.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
                if (newItem != null)
                {
                    BeginSelectionChange();
                    foreach (var item in Items) item.IsSelected = false;
                    foreach (var item in SidebarItems) item.IsSelected = (item == newItem);
                    EndSelectionChange();

                    newItem.RenameText = newItem.Name;
                    newItem.IsRenaming = true;
                }
            }
            else
            {
                pendingRenamePath = path;
                RequestRefresh();
            }
        }

        void UpdateItemPaths(string oldPath, string newPath)
        {
            var oldPrefix = oldPath + Path.DirectorySeparatorChar;

            foreach (var i in Items)
            {
                var iPath = i.Path.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(iPath, oldPath, StringComparison.OrdinalIgnoreCase))
                    i.UpdatePathAndName(newPath);
            }

            foreach (var s in SidebarItems)
            {
                var sPath = s.Path.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(sPath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    s.UpdatePathAndName(newPath);
                }
                else if (sPath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = sPath.Substring(oldPrefix.Length);
                    s.UpdatePathAndName(Path.Combine(newPath, rel));
                }
            }

            if (string.Equals(Location.TrimEnd(Path.DirectorySeparatorChar), oldPath, StringComparison.OrdinalIgnoreCase))
            {
                Location = newPath;
            }
            else if (Location.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var rel = Location.Substring(oldPrefix.Length);
                Location = Path.Combine(newPath, rel);
            }
        }

        async Task InitializeDrivesAsync()
        {
            var currentDrives = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
            var existingDrives = SidebarItems.Where(x => x.Level == 0).Select(x => x.Path).ToList();
            if (SidebarItems.Count(x => x.Level == 0) == currentDrives.Count && existingDrives.SequenceEqual(currentDrives)) return;

            var options = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            };

            var driveInfoList = await Task.Run(() =>
            {
                var result = new List<(string name, bool hasChild)>();
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    bool hasChild = false;
                    try { hasChild = new DirectoryInfo(drive.Name).EnumerateDirectories("*", options).Any(); } catch { }
                    result.Add((drive.Name, hasChild));
                }
                return result;
            });

            foreach (var item in SidebarItems)
                UnsubscribeSidebarItem(item);
            SidebarItems.Clear();
            foreach (var (name, hasChild) in driveInfoList)
                SidebarItems.Add(CreateSidebarItem(name, name, 0, hasChild));
        }

        ExplorerSidebarDirectoryViewModel CreateSidebarItem(string path, string name, int level, bool hasChild = true)
        {
            var vm = new ExplorerSidebarDirectoryViewModel(path, name, level, hasChild);
            vm.PropertyChanged += ItemViewModel_PropertyChanged;
            vm.ExpandRequested += OnSidebarItemExpandRequested;
            vm.CollapseRequested += OnSidebarItemCollapseRequested;
            return vm;
        }

        void UnsubscribeSidebarItem(ExplorerSidebarDirectoryViewModel vm)
        {
            vm.PropertyChanged -= ItemViewModel_PropertyChanged;
            vm.ExpandRequested -= OnSidebarItemExpandRequested;
            vm.CollapseRequested -= OnSidebarItemCollapseRequested;
            vm.Dispose();
        }

        async void OnSidebarItemExpandRequested(object? sender, EventArgs e)
        {
            if (sender is not ExplorerSidebarDirectoryViewModel vm) return;
            await ExpandSidebarItemAsync(vm);
        }

        void OnSidebarItemCollapseRequested(object? sender, EventArgs e)
        {
            if (sender is not ExplorerSidebarDirectoryViewModel vm) return;
            CollapseSidebarItem(vm);
        }

        async Task ExpandSidebarItemAsync(ExplorerSidebarDirectoryViewModel vm)
        {
            if (vm.IsExpanding) return;
            vm.IsExpanding = true;
            try
            {
                var options = new EnumerationOptions()
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                };

                var dirsWithChildren = await Task.Run(() =>
                {
                    var list = new List<(DirectoryInfo dir, bool hasChild)>();
                    try
                    {
                        var di = new DirectoryInfo(vm.Path);
                        foreach (var dir in di.EnumerateDirectories("*", options).OrderBy(d => d.Name))
                        {
                            bool hasChild = false;
                            try { hasChild = dir.EnumerateDirectories("*", options).Any(); }
                            catch { }
                            list.Add((dir, hasChild));
                        }
                    }
                    catch { }
                    return list;
                });

                int index = SidebarItems.IndexOf(vm);
                if (index < 0) return;

                if (index + 1 < SidebarItems.Count && SidebarItems[index + 1].Level > vm.Level)
                {
                    // Children already exist (e.g. previous expansion was cancelled before marking expanded).
                    vm.SetHasDummyChild(dirsWithChildren.Count > 0);
                    return;
                }

                int i = 1;
                foreach (var item in dirsWithChildren)
                {
                    var child = CreateSidebarItem(item.dir.FullName, item.dir.Name, vm.Level + 1, item.hasChild);
                    SidebarItems.Insert(index + i, child);
                    i++;
                }

                vm.SetHasDummyChild(dirsWithChildren.Count > 0);
            }
            catch (Exception e)
            {
                Log.Default.Write("ExpandSidebarItemAsync", e);
                vm.SetHasDummyChild(false);
            }
            finally
            {
                vm.IsExpanding = false;
            }
        }

        void CollapseSidebarItem(ExplorerSidebarDirectoryViewModel vm)
        {
            int index = SidebarItems.IndexOf(vm);
            if (index < 0) return;

            while (index + 1 < SidebarItems.Count && SidebarItems[index + 1].Level > vm.Level)
            {
                var child = SidebarItems[index + 1];
                UnsubscribeSidebarItem(child);
                SidebarItems.RemoveAt(index + 1);
            }
        }

        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExplorerLayout.Mode))
            {
                NotifyLayoutModeProperties();
            }
            if (e.PropertyName == nameof(ExplorerLayout.IconSize))
            {
                UpdateItemsImageSize();
            }
        }

        private void NotifyLayoutModeProperties()
        {
            OnPropertyChanged(nameof(IsViewList));
            OnPropertyChanged(nameof(IsViewWrapList));
            OnPropertyChanged(nameof(IsViewTiles));
        }

        private void Favorites_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (ExplorerFavorite item in e.OldItems) PropertyChangedEventManager.RemoveHandler(item, Favorite_PropertyChanged, string.Empty);
            }
            if (e.NewItems is not null)
            {
                foreach (ExplorerFavorite item in e.NewItems) PropertyChangedEventManager.AddHandler(item, Favorite_PropertyChanged, string.Empty);
            }
            RefreshFavoriteUrls();
        }

        private void Favorite_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshFavoriteUrls();

        private void RefreshFavoriteUrls()
        {
            FavoriteUrls.Clear();
            foreach (var fav in ExplorerSettings.Default.Favorites)
                FavoriteUrls.Add(new AddressBarSuggestion(fav.Name, fav.Url, fav.Url, AddressBarSuggestionSource.External));
        }

        private void Filter_FilterChanged(object? sender, EventArgs e) => UpdateFilteredItems();

        private void UpdateItemsImageSize()
        {
            var dpiScale = Math.Max(lastDpiScale.DpiScaleX, lastDpiScale.DpiScaleY);
            if (dpiScale <= 0) dpiScale = 1.0;
            foreach (var item in Items)
                item.SetImageSize((int)(Layout.IconSize * dpiScale), (int)(300 * dpiScale));
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

        void StartSidebarSync(string targetLocation, CancellationToken parentToken)
        {
            sidebarSyncCts?.Cancel();
            sidebarSyncCts?.Dispose();
            sidebarSyncCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken, disposeCts.Token);
            _ = SyncSidebarToLocationAsync(targetLocation, sidebarSyncCts.Token);
        }

        private void UpdateFilteredItems()
        {
            var targetList = Filter.IsFiltered ? Items.Where(item => Filter.IsMatch(item)).ToList() : Items.ToList();
            FilteredItems = [.. targetList.OrderBy(x => x, new ExplorerItemComparer(SortKey, SortOrder))];
        }

        bool isSkipUpdateSelection = false;
        void BeginSelectionChange() => isSkipUpdateSelection = true;
        void EndSelectionChange()
        {
            isSkipUpdateSelection = false;
            UpdateSelectedItems();
            UpdateSelectedPaths();
            RaiseCommandExecutable();
        }

        private void UpdateSelectedItems()
        {
            if (isSkipUpdateSelection) return;

            if (isSidebarLastActive)
            {
                var sidebarSelected = SidebarItems.Where(item => item.IsSelected).ToArray();
                if (sidebarSelected.Length > 0)
                {
                    SelectedItems = sidebarSelected;
                    return;
                }
                isSidebarLastActive = false;
            }

            SelectedItems = [.. FilteredItems.Where(item => item.IsSelected)];
        }

        private IExplorerSelectableItem[] GetActiveSelectedItems(object? parameter)
        {
            if (parameter is ExplorerSidebarDirectoryViewModel)
                return [.. SidebarItems.Where(x => x.IsSelected)];
            if (parameter is IExplorerItemViewModel)
                return [.. FilteredItems.Where(x => x.IsSelected)];
            return SelectedItems;
        }

        private string[] GetActiveSelectedPaths(object? parameter)
        {
            return GetActiveSelectedItems(parameter).Select(x => x.Path).ToArray();
        }

        private void UpdateSelectedPaths()
        {
            if (isSkipUpdateSelection) return;
            SelectedPaths = [.. SelectedItems.Select(item => item.Path)];
        }

        void RequestRefresh() => refreshDebouncer.Signal(null);

        void StopFileSystemWatcher()
        {
            if (watcher is null) return;
            watcher.Created -= Watcher_Callback;
            watcher.Deleted -= Watcher_Callback;
            watcher.Renamed -= Watcher_Callback;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            watcher = null;
        }

        async Task RefreshInternalAsync()
        {
            var oldCts = refreshCts;
            oldCts?.Cancel();
            refreshCts = new CancellationTokenSource();
            oldCts?.Dispose();

            isLoading = true;
            RaiseCommandExecutable();

            var token = refreshCts.Token;
            try
            {
                await RefreshCoreAsync(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Default.Write("ExplorerViewModel.Refresh", e);
            }
            finally
            {
                isLoading = false;
                RaiseCommandExecutable();
            }
        }

        async Task RefreshCoreAsync(CancellationToken token)
        {
            var currentLocation = Location;
            if (watcher != null && !string.Equals(watcher.Path, currentLocation, StringComparison.OrdinalIgnoreCase))
            {
                StopFileSystemWatcher();
            }

            await InitializeDrivesAsync();
            Application.Current.Dispatcher.Invoke(() => StartSidebarSync(currentLocation, token));

            if (!TryCheckFileSystemAccess(currentLocation, true)) return;

            var options = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            };

            var (dirsInfo, filesInfo) = await Task.Run(() =>
            {
                var di = new DirectoryInfo(currentLocation);
                var d = new List<(DirectoryInfo dir, bool hasChild)>();
                foreach (var dir in di.EnumerateDirectories("*", options))
                {
                    token.ThrowIfCancellationRequested();
                    bool hasChild = false;
                    try { hasChild = dir.EnumerateDirectories("*", options).Any(); } catch { }
                    d.Add((dir, hasChild));
                }

                var f = new List<FileInfo>();
                foreach (var file in di.EnumerateFiles("*", options))
                {
                    token.ThrowIfCancellationRequested();
                    f.Add(file);
                }
                return (d, f);
            }, token);

            token.ThrowIfCancellationRequested();

            if (Location != currentLocation) return;

            var oldItemsMap = Items.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
            var newItemsList = new List<IExplorerItemViewModel>(dirsInfo.Count + filesInfo.Count);

            foreach (var item in dirsInfo)
            {
                var d = item.dir;
                if (oldItemsMap.TryGetValue(d.FullName, out var oldItem) && oldItem is ExplorerDirectoryItemViewModel oldDir)
                {
                    if (oldDir.LastWriteTime == d.LastWriteTime)
                    {
                        newItemsList.Add(oldDir);
                        oldItemsMap.Remove(d.FullName);
                    }
                    else
                    {
                        newItemsList.Add(new ExplorerDirectoryItemViewModel(d.FullName, d.LastWriteTime)
                        {
                            IsSelected = oldDir.IsSelected,
                            IsRenaming = oldDir.IsRenaming,
                            RenameText = oldDir.RenameText
                        });
                    }
                }
                else
                {
                    newItemsList.Add(new ExplorerDirectoryItemViewModel(d.FullName, d.LastWriteTime));
                }
            }

            foreach (var f in filesInfo)
            {
                if (oldItemsMap.TryGetValue(f.FullName, out var oldItem) && oldItem is ExplorerFileItemViewModel oldFile)
                {
                    if (oldFile.LastWriteTime == f.LastWriteTime)
                    {
                        newItemsList.Add(oldFile);
                        oldItemsMap.Remove(f.FullName);
                    }
                    else
                    {
                        newItemsList.Add(new ExplorerFileItemViewModel(f.FullName, f.LastWriteTime)
                        {
                            IsSelected = oldFile.IsSelected,
                            IsRenaming = oldFile.IsRenaming,
                            RenameText = oldFile.RenameText
                        });
                    }
                }
                else
                {
                    newItemsList.Add(new ExplorerFileItemViewModel(f.FullName, f.LastWriteTime));
                }
            }

            if (pendingRenamePath != null)
            {
                var target = newItemsList.FirstOrDefault(x => string.Equals(x.Path, pendingRenamePath, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    target.IsRenaming = true;
                    target.RenameText = target.Name;
                    target.IsSelected = true;
                }
                pendingRenamePath = null;
            }

            var dpiScale = Math.Max(lastDpiScale.DpiScaleX, lastDpiScale.DpiScaleY);
            if (dpiScale <= 0) dpiScale = 1.0;

            foreach (var item in newItemsList)
            {
                if (!Items.Contains(item))
                {
                    item.PropertyChanged += ItemViewModel_PropertyChanged;
                }
                item.SetImageSize((int)(Layout.IconSize * dpiScale), (int)(300 * dpiScale));
            }

            BeginSelectionChange();
            try
            {
                Items = [.. newItemsList];
            }
            finally
            {
                EndSelectionChange();
            }

            foreach (var removed in oldItemsMap.Values)
            {
                removed.PropertyChanged -= ItemViewModel_PropertyChanged;
                removed.Dispose();
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateFilteredItems();

                var currentSidebarItem = SidebarItems.FirstOrDefault(x =>
                    string.Equals(x.Path.TrimEnd(Path.DirectorySeparatorChar),
                                  currentLocation.TrimEnd(Path.DirectorySeparatorChar),
                                  StringComparison.OrdinalIgnoreCase));
                if (currentSidebarItem != null)
                {
                    if (currentSidebarItem.IsExpanded)
                    {
                        SyncSidebarChildren(currentSidebarItem, dirsInfo);
                    }
                    else
                    {
                        currentSidebarItem.SetHasDummyChild(dirsInfo.Count > 0);
                    }
                }
            });

            if (watcher == null)
            {
                try
                {
                    watcher = new FileSystemWatcher(currentLocation)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    };
                    watcher.Created += Watcher_Callback;
                    watcher.Deleted += Watcher_Callback;
                    watcher.Renamed += Watcher_Callback;
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception e)
                {
                    Log.Default.Write("WatcherException", e);
                    watcher?.Dispose();
                    watcher = null;
                }
            }
        }

        async Task SyncSidebarToLocationAsync(string targetLocation, CancellationToken token)
        {
            if (string.IsNullOrEmpty(targetLocation)) return;

            var segments = BuildLocationSegments(targetLocation);
            if (segments.Count == 0) return;

            ExplorerSidebarDirectoryViewModel? target = null;
            bool targetWasFreshlyExpanded = false;

            for (int i = 0; i < segments.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var segmentPath = segments[i];
                var isLast = i == segments.Count - 1;

                var match = SidebarItems.FirstOrDefault(x =>
                    string.Equals(x.Path.TrimEnd(Path.DirectorySeparatorChar),
                                  segmentPath.TrimEnd(Path.DirectorySeparatorChar),
                                  StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    if (target != null && target.IsExpanded && !targetWasFreshlyExpanded)
                    {
                        CollapseSidebarItem(target);
                        target.ResetExpandedState();
                        await ExpandSidebarItemAsync(target);
                        token.ThrowIfCancellationRequested();
                        target.MarkExpanded();

                        targetWasFreshlyExpanded = true;

                        match = SidebarItems.FirstOrDefault(x =>
                            string.Equals(x.Path.TrimEnd(Path.DirectorySeparatorChar),
                                          segmentPath.TrimEnd(Path.DirectorySeparatorChar),
                                          StringComparison.OrdinalIgnoreCase));
                    }

                    if (match == null) return;
                }

                bool wasExpanded = match.IsExpanded;
                if (!isLast && !wasExpanded)
                {
                    await ExpandSidebarItemAsync(match);
                    token.ThrowIfCancellationRequested();
                    match.MarkExpanded();
                }

                target = match;
                targetWasFreshlyExpanded = !wasExpanded;
            }

            if (target == null) return;

            BeginSelectionChange();
            try
            {
                foreach (var item in SidebarItems)
                    item.IsSelected = false;
                target.IsSelected = true;
            }
            finally
            {
                EndSelectionChange();
            }
        }

        static List<string> BuildLocationSegments(string location)
        {
            try
            {
                var segments = new List<string>();
                var current = Path.GetFullPath(location);
                while (true)
                {
                    segments.Insert(0, current);
                    var parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(parent) ||
                        string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = parent;
                }
                return segments;
            }
            catch
            {
                return [];
            }
        }



        private void Watcher_Callback(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType is WatcherChangeTypes.Deleted)
            {
                Application.Current.Dispatcher.InvokeAsync(() => RemoveSidebarItemsByPath(e.FullPath));
            }
            else if (e.ChangeType is WatcherChangeTypes.Renamed && e is RenamedEventArgs renamed)
            {
                Application.Current.Dispatcher.InvokeAsync(() => RemoveSidebarItemsByPath(renamed.OldFullPath));
            }

            if (e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Deleted or WatcherChangeTypes.Renamed)
                refreshDebouncer.Signal(null);
        }

        void RemoveSidebarItemsByPath(string removedPath)
        {
            var prefix = removedPath.TrimEnd(Path.DirectorySeparatorChar);
            var prefixSlash = prefix + Path.DirectorySeparatorChar;

            for (int i = SidebarItems.Count - 1; i >= 0; i--)
            {
                var item = SidebarItems[i];
                var itemPath = item.Path.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(itemPath, prefix, StringComparison.OrdinalIgnoreCase)
                    || itemPath.StartsWith(prefixSlash, StringComparison.OrdinalIgnoreCase))
                {
                    UnsubscribeSidebarItem(item);
                    SidebarItems.RemoveAt(i);
                }
            }
        }

        void SyncSidebarChildren(ExplorerSidebarDirectoryViewModel parent, List<(DirectoryInfo dir, bool hasChild)> dirs)
        {
            if (!parent.IsExpanded) return;

            int parentIndex = SidebarItems.IndexOf(parent);
            if (parentIndex < 0) return;

            var existingChildren = new List<ExplorerSidebarDirectoryViewModel>();
            int i = parentIndex + 1;
            while (i < SidebarItems.Count && SidebarItems[i].Level > parent.Level)
            {
                if (SidebarItems[i].Level == parent.Level + 1)
                {
                    existingChildren.Add(SidebarItems[i]);
                }
                i++;
            }

            var newDirsSet = dirs.Select(d => d.dir.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var child in existingChildren)
            {
                if (!newDirsSet.Contains(child.Path))
                {
                    RemoveSidebarItemsByPath(child.Path);
                }
            }

            var orderedDirs = dirs.OrderBy(d => d.dir.Name).ToList();
            int insertIndex = SidebarItems.IndexOf(parent) + 1;
            
            foreach (var item in orderedDirs)
            {
                bool found = false;
                int searchIndex = insertIndex;
                while (searchIndex < SidebarItems.Count && SidebarItems[searchIndex].Level > parent.Level)
                {
                    if (SidebarItems[searchIndex].Level == parent.Level + 1 &&
                        string.Equals(SidebarItems[searchIndex].Path, item.dir.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                    searchIndex++;
                }

                if (found)
                {
                    var existingChild = SidebarItems[searchIndex];
                    if (!item.hasChild && existingChild.IsExpanded)
                    {
                        CollapseSidebarItem(existingChild);
                        existingChild.ResetExpandedState();
                    }
                    existingChild.SetHasDummyChild(item.hasChild);

                    insertIndex = searchIndex + 1;
                    while (insertIndex < SidebarItems.Count && SidebarItems[insertIndex].Level > parent.Level + 1)
                    {
                        insertIndex++;
                    }
                }
                else
                {
                    var newChild = CreateSidebarItem(item.dir.FullName, item.dir.Name, parent.Level + 1, item.hasChild);
                    SidebarItems.Insert(insertIndex, newChild);
                    insertIndex++;
                }
            }
            
            parent.SetHasDummyChild(orderedDirs.Count > 0);
        }

        internal void ItemViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IExplorerSelectableItem.IsSelected))
            {
                if (!isSkipUpdateSelection && sender is IExplorerSelectableItem item && item.IsSelected)
                {
                    isSidebarLastActive = item is ExplorerSidebarDirectoryViewModel;
                }

                UpdateSelectedItems();
                RaiseCommandExecutable();
            }
        }

        void RaiseCommandExecutable()
        {
            foreach (var command in Commands) command.RaiseCanExecuteChanged();
        }

        public void LoadState(ToolState stateData)
        {
            var savedState = stateData.SavedState;
            if (string.IsNullOrEmpty(savedState)) return;
            var state = Json.Json.LoadFromText<ExplorerState>(savedState);
            if (state is null) return;
            Location = state.Location;
            Layout = state.Layout;
            Filter.CopyFrom(state.Filter);
            SortKey = state.SortKey;
            SortOrder = state.SortOrder;
            RequestRefresh();
            UpdateFilteredItems();
        }

        public ToolState SaveState() => new()
        {
            SavedState = Json.Json.GetJsonText(new ExplorerState(Location, Layout.Clone(), Filter) { SortKey = SortKey, SortOrder = SortOrder })
        };

        static string? ResolveNavigationTarget(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            if (IsShortcutFile(raw))
            {
                var resolved = ShellLink.ResolveShortcut(raw);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
                return null;
            }

            return raw;
        }

        static bool IsShortcutFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                return string.Equals(Path.GetExtension(path.Trim()), ".lnk", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException e)
            {
                Log.Default.Write("IsShortcutFile", e);
                return false;
            }
        }

        static bool TryCheckFileSystemAccess(string? path, bool? requireDirectory)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                var attr = File.GetAttributes(path);
                if (requireDirectory.HasValue)
                {
                    return requireDirectory.Value
                        ? attr.HasFlag(FileAttributes.Directory)
                        : !attr.HasFlag(FileAttributes.Directory);
                }
                return true;
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Default.Write($"TryCheckFileSystemAccess UnauthorizedAccess path={path}", e);
                return false;
            }
            catch (Exception e)
            {
                Log.Default.Write($"TryCheckFileSystemAccess Exception path={path}", e);
                return false;
            }
        }

        static void StartProcessSafe(string fileName, string arguments)
        {
            try
            {
                System.Diagnostics.Process.Start(fileName, arguments);
            }
            catch (Exception e)
            {
                Log.Default.Write($"アプリケーションの起動に失敗しました。fileName={fileName} arguments={arguments}", e);
            }
        }

        static string BuildArchiveName(string[] paths)
        {
            if (paths.Length == 1)
            {
                var targetPath = paths[0].TrimEnd(Path.DirectorySeparatorChar);
                if (Directory.Exists(targetPath))
                    return Path.GetFileName(targetPath);
                return Path.GetFileNameWithoutExtension(targetPath);
            }
            return "archive";
        }

        static void AddDirectoryToArchive(ZipArchive archive, string directoryPath, string entryPrefix)
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.Combine(entryPrefix, Path.GetRelativePath(directoryPath, file)).Replace('\\', '/');
                archive.CreateEntryFromFile(file, relative);
            }

            foreach (var dir in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    var relative = Path.Combine(entryPrefix, Path.GetRelativePath(directoryPath, dir)).Replace('\\', '/') + "/";
                    archive.CreateEntry(relative);
                }
            }
        }

        static void RunOnStaThread(Action action)
        {
            var thread = new Thread(() => action())
            {
                IsBackground = true,
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        static string GetTitle(string location)
        {
            if (string.IsNullOrEmpty(location)) return Texts.Explorer;
            try
            {
                var name = Path.GetFileName(location.TrimEnd(Path.DirectorySeparatorChar));
                return string.IsNullOrEmpty(name) ? location : name;
            }
            catch (ArgumentException e)
            {
                Log.Default.Write("GetTitle", e);
                return Texts.Explorer;
            }
        }

        IExplorerSelectableItem? GetActiveItem(object? parameter)
        {
            if (parameter is IExplorerSelectableItem item)
                return item;
            if (SelectedItems.Length == 1)
                return SelectedItems[0];
            return null;
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
                    this.PropertyChanged -= ExplorerViewModel_PropertyChanged;
                    Filter.FilterChanged -= Filter_FilterChanged;
                    CollectionChangedEventManager.RemoveHandler(ExplorerSettings.Default.Favorites, Favorites_CollectionChanged);
                    foreach (var favorite in ExplorerSettings.Default.Favorites)
                        PropertyChangedEventManager.RemoveHandler(favorite, Favorite_PropertyChanged, string.Empty);

                    if (Layout != null) Layout.PropertyChanged -= Layout_PropertyChanged;

                    foreach (var item in Items)
                    {
                        item.PropertyChanged -= ItemViewModel_PropertyChanged;
                        item.Dispose();
                    }

                    foreach (var item in SidebarItems)
                        UnsubscribeSidebarItem(item);

                    disposeCts.Cancel();
                    disposeCts.Dispose();
                    refreshCts?.Cancel();
                    refreshCts?.Dispose();
                    sidebarSyncCts?.Cancel();
                    sidebarSyncCts?.Dispose();
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
