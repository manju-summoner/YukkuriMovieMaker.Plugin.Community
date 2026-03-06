using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class NodeEditorViewModel : INotifyPropertyChanged
{
    public NodeEditorViewModel()
    {
        Tabs = [];

        // コマンド初期化
        AddNodeCommand = new RelayCommand(ShowAddNodeMenu, () => SelectedTab != null);
        FitToScreenCommand = new RelayCommand(FitToScreen, () => SelectedTab != null);
        ResetZoomCommand = new RelayCommand(ResetZoom, () => SelectedTab != null);
    }

    public ObservableCollection<TabViewModel> Tabs { get; }

    public TabViewModel? SelectedTab
    {
        get;
        set => SetField(ref field, value);
    }

    public ICommand AddNodeCommand { get; }
    public ICommand FitToScreenCommand { get; }
    public ICommand ResetZoomCommand { get; }

    public GraphControlMode CurrentMode
    {
        get;
        set => SetField(ref field, value);
    } = GraphControlMode.RectSelection;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OpenGraph(NodeGraph graph, string title = "Main")
    {
        var mainTab = new TabViewModel(graph, title, this, CloseTab);
        if (Tabs.Contains(mainTab))
        {
            SelectedTab = mainTab;
            return;
        }

        Tabs.Add(mainTab);
        SelectedTab = mainTab;
    }

    private void ShowAddNodeMenu()
    {
        if (SelectedTab == null)
            return;

        // TODO: ノード追加メニューの実装
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