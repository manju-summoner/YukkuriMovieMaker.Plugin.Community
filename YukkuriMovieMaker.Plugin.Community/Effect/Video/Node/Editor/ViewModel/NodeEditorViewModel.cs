using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class NodeEditorViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, List<NodeTypeInfo>> _nodeCategories;

    private GraphSnapshot? _clipboard;

    public NodeEditorViewModel()
    {
        Tabs = [];

        _nodeCategories = CollectNodeTypes();

        // コマンド初期化
        AddNodeCommand = new RelayCommand<Point?>(ShowAddNodeMenu, _ => SelectedTab != null);
        FitToScreenCommand = new RelayCommand(FitToScreen, () => SelectedTab != null);
        ResetZoomCommand = new RelayCommand(ResetZoom, () => SelectedTab != null);
        ZoomUpCommand = new RelayCommand(ZoomUp, () => SelectedTab != null);
        ZoomDownCommand = new RelayCommand(ZoomDown, () => SelectedTab != null);
        CopyCommand = new RelayCommand(Copy, () => SelectedTab?.GraphViewModel.SelectedNodes.Count > 0);
        CutCommand = new RelayCommand(Cut, () => SelectedTab?.GraphViewModel.SelectedNodes.Count > 0);
        PasteCommand = new RelayCommand(Paste, () => _clipboard != null);
        DeleteSelectedCommand =
            new RelayCommand(DeleteSelected, () => SelectedTab?.GraphViewModel.SelectedNodes.Count > 0);
    }

    public ObservableCollection<TabViewModel> Tabs { get; }

    public TabViewModel? SelectedTab
    {
        get;
        set => SetField(ref field, value);
    }

    public ICommand AddNodeCommand { get; private set; }
    public ICommand FitToScreenCommand { get; private set; }
    public ICommand ResetZoomCommand { get; private set; }
    public ICommand ZoomUpCommand { get; private set; }
    public ICommand ZoomDownCommand { get; private set; }
    public ICommand CopyCommand { get; private set; }
    public ICommand CutCommand { get; private set; }
    public ICommand PasteCommand { get; private set; }
    public ICommand DeleteSelectedCommand { get; private set; }

    public GraphControlMode CurrentMode
    {
        get;
        set => SetField(ref field, value);
    } = GraphControlMode.RectSelection;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? GraphUpdated;

    public void OnGraphUpdated()
    {
        GraphUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void OpenGraph(NodeGraph graph, string title = "Main")
    {
        if (Tabs.Any(tab => tab.Graph == graph))
        {
            SelectedTab = Tabs.First(tab => tab.Graph == graph);
            return;
        }

        var mainTab = new TabViewModel(graph, title, this, CloseTab);
        Tabs.Add(mainTab);
        SelectedTab = mainTab;
    }

    private void ShowAddNodeMenu(Point? mousePosition)
    {
        if (SelectedTab == null)
            return;

        var menu = new ContextMenu();

        var categoryMap = new Dictionary<string, MenuItem>();

        foreach (var category in _nodeCategories.OrderBy(c => c.Key))
        {
            var parts = category.Key.Split('/');

            var currentPath = "";
            MenuItem? parent = null;

            foreach (var part in parts)
            {
                currentPath = currentPath.Length == 0 ? part : currentPath + "/" + part;

                if (!categoryMap.TryGetValue(currentPath, out var currentItem))
                {
                    currentItem = new MenuItem
                    {
                        Header = part
                    };

                    categoryMap[currentPath] = currentItem;

                    if (parent == null)
                        menu.Items.Add(currentItem);
                    else
                        parent.Items.Add(currentItem);
                }

                parent = currentItem;
            }

            foreach (var nodeInfo in category.Value.OrderBy(n => n.Label))
            {
                var nodeItem = new MenuItem
                {
                    Header = nodeInfo.Label,
                    ToolTip = nodeInfo.Description,
                    Command = SelectedTab.GraphViewModel.AddNodeCommand,
                    CommandParameter = nodeInfo.Type,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Icon = new Rectangle
                    {
                        Width = 5,
                        Height = 12,
                        Fill = ColorToBrushConverter.Convert(nodeInfo.Color)
                    }
                };

                parent!.Items.Add(nodeItem);
            }
        }

        menu.IsOpen = true;
    }

    private static Dictionary<string, List<NodeTypeInfo>> CollectNodeTypes()
    {
        var categories = new Dictionary<string, List<NodeTypeInfo>>();

        var assembly = Assembly.GetExecutingAssembly();
        var nodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(NodeLogic)) && !t.IsAbstract)
            .Where(t => t != typeof(ArgumentsNode) && t != typeof(ReturnNode))
            .Select(t => new
            {
                Type = t,
                Attr = t.GetCustomAttribute<NodeAttribute>()
            })
            .Where(x => x.Attr != null)
            .Select(x => new
            {
                x.Type,
                x.Attr,
                CtgrInst = (INodeCategory)Activator.CreateInstance(x.Attr!.CategoryType)!
            })
            .Select(x => new NodeTypeInfo
            {
                Type = x.Type,
                Category = x.CtgrInst.Category,
                Label = x.Attr!.Label,
                Description = x.Attr!.Description,
                Color = x.CtgrInst.Color
            });

        foreach (var nodeInfo in nodeTypes)
        {
            if (!categories.TryGetValue(nodeInfo.Category, out var value))
            {
                value = [];
                categories[nodeInfo.Category] = value;
            }

            value.Add(nodeInfo);
        }

        return categories;
    }

    private void CloseTab(TabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (SelectedTab == tab)
        {
            if (Tabs.Count > 0)
            {
                var newIndex = Math.Max(0, Math.Min(index, Tabs.Count - 1));
                SelectedTab = Tabs[newIndex];
            }
            else
            {
                SelectedTab = null;
            }
        }
    }

    private void FitToScreen()
    {
        if (SelectedTab == null)
            return;

        var graphVm = SelectedTab.GraphViewModel;

        if (graphVm.Nodes.Count == 0)
        {
            ResetZoom();
            return;
        }

        var minX = graphVm.Nodes.Min(n => n.X);
        var minY = graphVm.Nodes.Min(n => n.Y);
        var maxX = graphVm.Nodes.Max(n => n.X + n.Width);
        var maxY = graphVm.Nodes.Max(n => n.Y + n.Height);

        var width = maxX - minX;
        var height = maxY - minY;

        const double padding = 50;
        var paddedWidth = width + padding * 2;
        var paddedHeight = height + padding * 2;

        var viewportWidth = graphVm.Width;
        var viewportHeight = graphVm.Height;

        var zoomX = viewportWidth / paddedWidth;
        var zoomY = viewportHeight / paddedHeight;
        var zoom = Math.Min(zoomX, zoomY);

        zoom = Math.Max(0.1, Math.Min(5.0, zoom));

        graphVm.Zoom = zoom;

        graphVm.PanX = (viewportWidth - width * zoom) / 2 - minX * zoom;
        graphVm.PanY = (viewportHeight - height * zoom) / 2 - minY * zoom;
    }

    private void ResetZoom()
    {
        if (SelectedTab == null)
            return;

        var graphVm = SelectedTab.GraphViewModel;
        graphVm.Zoom = 1.0;
        graphVm.PanX = 0;
        graphVm.PanY = 0;
    }

    private void ZoomUp()
    {
        if (SelectedTab == null)
            return;

        var graphVm = SelectedTab.GraphViewModel;
        var oldZoom = graphVm.Zoom;
        var newZoom = oldZoom * 1.1;

        if (newZoom < 0.1) newZoom = 0.1;
        if (newZoom > 5.0) newZoom = 5.0;

        var cx = graphVm.Width / 2;
        var cy = graphVm.Height / 2;

        graphVm.Zoom = newZoom;
        graphVm.PanX = cx - (cx - graphVm.PanX) * (newZoom / oldZoom);
        graphVm.PanY = cy - (cy - graphVm.PanY) * (newZoom / oldZoom);
    }

    private void ZoomDown()
    {
        if (SelectedTab == null)
            return;

        var graphVm = SelectedTab.GraphViewModel;
        var oldZoom = graphVm.Zoom;
        var newZoom = oldZoom / 1.1;

        if (newZoom < 0.1) newZoom = 0.1;
        if (newZoom > 5.0) newZoom = 5.0;

        var cx = graphVm.Width / 2;
        var cy = graphVm.Height / 2;

        graphVm.Zoom = newZoom;
        graphVm.PanX = cx - (cx - graphVm.PanX) * (newZoom / oldZoom);
        graphVm.PanY = cy - (cy - graphVm.PanY) * (newZoom / oldZoom);
    }

    private void Copy()
    {
        if (SelectedTab == null) return;
        SelectedTab.GraphViewModel.Copy(out var clipboard);
        if (clipboard != null)
            _clipboard = clipboard;
    }

    private void Paste()
    {
        SelectedTab?.GraphViewModel.ClearSelection();
        SelectedTab?.GraphViewModel.Paste(_clipboard);
    }

    private void Cut()
    {
        Copy();
        DeleteSelected();
    }

    private void DeleteSelected()
    {
        SelectedTab?.GraphViewModel.DeleteSelectedNode();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}