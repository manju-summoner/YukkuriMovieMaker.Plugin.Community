using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using VisualStateChangedEventArgs =
    YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events.VisualStateChangedEventArgs;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class GraphViewModel : INotifyPropertyChanged
{
    private readonly NodeGraph _graph;

    public GraphViewModel(NodeGraph graph, NodeEditorViewModel nodeEditorViewModel, IEditorInfo? editorInfo = null)
    {
        _graph = graph;
        ParentEditor = nodeEditorViewModel;
        EditorInfo = editorInfo;

        _graph.GraphChanged += OnGraphChanged;
        nodeEditorViewModel.GraphUpdated += (_, _) => SyncFromGraph();

        SyncFromGraph();

        AddNodeCommand = new RelayCommand<Type>(AddNode);
        DeleteNodeCommand = new RelayCommand<Guid>(DeleteNode);
        ConnectPortsCommand = new RelayCommand<(PortViewModel From, PortViewModel To)>(ConnectPorts);
        DisconnectCommand = new RelayCommand<ConnectionViewModel>(Disconnect);
    }

    public NodeEditorViewModel ParentEditor { get; }
    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>
    ///     このグラフを開いた OpenNodeEditorButton (IPropertyEditorControl2.SetEditorInfo) から
    ///     渡された IEditorInfo。Node Editor パネルを直接操作しているだけの場合や、
    ///     まだ一度もアイテム経由で開かれていない場合は null。
    ///     値が変わっても既存の NodeViewModel/PortViewModel には遡って反映されない
    ///     （SyncFromGraph による再構築のタイミングで新しい値が反映される）。
    /// </summary>
    public IEditorInfo? EditorInfo
    {
        get;
        internal set => SetField(ref field, value);
    }

    public PortViewModel? DraggingFromPort
    {
        get;
        set => SetField(ref field, value);
    }

    public Point? TemporaryEndPoint
    {
        get;
        set => SetField(ref field, value);
    }

    public Point? PendingContextPoint
    {
        get;
        set => SetField(ref field, value);
    }

    public ICommand AddNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand ConnectPortsCommand { get; }
    public ICommand DisconnectCommand { get; }

    public ObservableCollection<NodeViewModel> SelectedNodes { get; } = [];

    public double Zoom
    {
        get;
        set => SetField(ref field, value);
    } = 1.0;

    public double PanX
    {
        get;
        set => SetField(ref field, value);
    } = 0.0;

    public double PanY
    {
        get;
        set => SetField(ref field, value);
    } = 0.0;

    public double Width { get; set; }

    public double Height { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     グラフの現在状態から Nodes/Connections を作り直す。
    ///     通常は GraphChanged/GraphUpdated イベント経由で自動的に呼ばれるが、
    ///     Undo/Redo がプロパティセッタを経由せずグラフを書き換えるような実装だと、
    ///     そのイベントチェーンが発火せず、パネルの表示が古いままになることがある。
    ///     そのための保険として、パネルがアクティブになったタイミングなどで
    ///     外部から明示的に呼び直せるようにしておく。
    /// </summary>
    public void Refresh()
    {
        SyncFromGraph();
    }

    private void UpdatePortValue(ValueChangedEventArgs e)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == e.NodeId);
        if (node == null) return;

        var port = node.InputPorts.FirstOrDefault(p => p.Name == e.PortName);
        if (port != null)
            port.UpdateValueFromGraph(e.NewValue);
    }

    private void SyncFromGraph()
    {
        var nodeViewModels = new Dictionary<Guid, NodeViewModel>();

        // 破棄前に古い VM のイベント購読を解除する。NodeLogic はここで再生成されず
        // 使い回されるため、解除しないと同じ NodeLogic に購読が積み重なっていく。
        foreach (var oldVm in Nodes)
            oldVm.Dispose();

        Nodes.Clear();

        foreach (var node in _graph.Nodes.Values)
        {
            NodeViewModel vm;
            try
            {
                vm = new NodeViewModel(node, _graph, this);
            }
            catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
            {
                // 属性不備等で1ノードの構築に失敗しても、他のノードの表示は継続する。
                Debug.WriteLine($"[GraphViewModel] Failed to build NodeViewModel for {node.Id}: {ex}");
                continue;
            }

            Nodes.Add(vm);
            nodeViewModels[node.Id] = vm;
        }

        foreach (var node in Nodes)
        foreach (var port in node.InputPorts)
            port.IsConnected = false;

        Connections.Clear();
        foreach (var conn in _graph.Connections)
        {
            if (!nodeViewModels.ContainsKey(conn.FromId) || !nodeViewModels.ContainsKey(conn.ToId))
                continue;

            var vm = new ConnectionViewModel(conn, nodeViewModels);
            Connections.Add(vm);
        }
    }

    private void OnGraphChanged(object? sender, GraphChangedEventArgs e)
    {
        switch (e)
        {
            case NodeAddedEventArgs added:
            {
                NodeViewModel vm;
                try
                {
                    vm = new NodeViewModel(added.Node, _graph, this);
                }
                catch (Exception ex) when (!ExceptionPolicy.IsFatal(ex))
                {
                    Debug.WriteLine($"[GraphViewModel] Failed to build NodeViewModel for {added.NodeId}: {ex}");
                    break;
                }

                Nodes.Add(vm);
                foreach (var port in vm.InputPorts)
                    port.ApplyDefaultToGraph();
                break;
            }

            case NodeRemovedEventArgs removed:
            {
                var node = Nodes.FirstOrDefault(n => n.Id == removed.NodeId);
                if (node != null)
                {
                    node.Dispose();
                    Nodes.Remove(node);
                }

                break;
            }

            case ConnectionChangedEventArgs:
            {
                SyncFromGraph();
                break;
            }

            case ValueChangedEventArgs valueChanged:
            {
                UpdatePortValue(valueChanged);
                break;
            }

            case VisualStateChangedEventArgs visualStateChanged:
            {
                var node = Nodes.FirstOrDefault(nodeViewModel => nodeViewModel.Id == visualStateChanged.NodeId);
                if (node == null) break;
                node.X = visualStateChanged.NewState.X;
                node.Y = visualStateChanged.NewState.Y;
                break;
            }
        }
    }

    private void AddNode(Type? nodeType)
    {
        if (nodeType == null) return;

        _graph.BeginEdit();
        try
        {
            var node = (NodeLogic)Activator.CreateInstance(nodeType)!;
            node.Id = Guid.NewGuid();

            _graph.AddNode(node);

            var pos = PendingContextPoint ?? new Point(100, 100);
            _graph.SetVisualState(node.Id, pos.X, pos.Y);
        }
        finally
        {
            // 途中で例外が起きても IsInTransaction が固着しないようにする。
            _graph.EndEdit();
        }
    }

    internal void DeleteSelectedNode()
    {
        _graph.BeginEdit();
        try
        {
            foreach (var guid in SelectedNodes
                         .Where(nodeVm =>
                             nodeVm.NodeLogic.GetType() != typeof(ArgumentsNode) &&
                             nodeVm.NodeLogic.GetType() != typeof(ReturnNode))
                         .Select(vm => vm.Id))
                _graph.RemoveNode(guid);
        }
        finally
        {
            _graph.EndEdit();
        }
    }

    private void DeleteNode(Guid nodeId)
    {
        _graph.BeginEdit();
        try
        {
            _graph.RemoveNode(nodeId);
        }
        finally
        {
            _graph.EndEdit();
        }
    }

    private void ConnectPorts((PortViewModel A, PortViewModel B) ports)
    {
        var p1 = ports.A;
        var p2 = ports.B;

        if (p1.Direction == p2.Direction)
            return;

        var from = p1.Direction == PortDirection.Output ? p1 : p2;
        var to = p1.Direction == PortDirection.Input ? p1 : p2;

        if (_graph.WouldCreateCycle(from.NodeId, to.NodeId))
            return;

        _graph.BeginEdit();
        try
        {
            _graph.Connect(
                from.NodeId, from.Name,
                to.NodeId, to.Name
            );
        }
        finally
        {
            _graph.EndEdit();
        }
    }

    private void Disconnect(ConnectionViewModel? connection)
    {
        if (connection == null) return;
        _graph.BeginEdit();
        try
        {
            _graph.Disconnect(
                connection.FromNodeId, connection.FromPortName,
                connection.ToNodeId, connection.ToPortName
            );
        }
        finally
        {
            _graph.EndEdit();
        }
    }

    public void ClearSelection()
    {
        foreach (var n in SelectedNodes)
            n.IsSelected = false;

        SelectedNodes.Clear();
    }

    public void ApplyRectSelection(Rect rect, bool additive)
    {
        if (!additive)
            ClearSelection();

        foreach (var node in Nodes)
        {
            var nodeRect = new Rect(node.X * Zoom + PanX, node.Y * Zoom + PanY, node.Width * Zoom, node.Height * Zoom);

            if (rect.IntersectsWith(nodeRect))
                AddToSelection(node);
        }
    }

    public void ApplyLassoSelection(IReadOnlyList<Point> polygon, bool additive)
    {
        if (polygon.Count < 3) return;

        if (!additive)
            ClearSelection();

        foreach (var node in Nodes)
        {
            var center = TransformToScreen(new Point(
                node.X + node.Width / 2,
                node.Y + node.Height / 2));

            if (IsPointInPolygon(center))
                AddToSelection(node);
        }

        return;

        bool IsPointInPolygon(Point p)
        {
            var inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                var intersect =
                    pi.Y > p.Y != pj.Y > p.Y &&
                    p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }
    }

    public void AddToSelection(NodeViewModel node)
    {
        if (SelectedNodes.Contains(node))
            return;

        SelectedNodes.Add(node);
        node.IsSelected = true;
    }

    public void SelectSingle(NodeViewModel node)
    {
        ClearSelection();
        AddToSelection(node);
    }

    public void BeginNodeDrag(NodeViewModel origin)
    {
        if (!SelectedNodes.Contains(origin)) SelectSingle(origin);
    }

    public void UpdateNodeDrag(Vector delta)
    {
        foreach (var nodeVm in SelectedNodes)
        {
            nodeVm.X += delta.X;
            nodeVm.Y += delta.Y;
        }
    }

    public void EndNodeDrag()
    {
        foreach (var nodeVm in SelectedNodes)
            nodeVm.CommitPosition();
    }

    public Point TransformToCanvas(Point screenPoint)
    {
        return new Point(
            (screenPoint.X - PanX) / Zoom,
            (screenPoint.Y - PanY) / Zoom
        );
    }

    public Point TransformToScreen(Point canvasPoint)
    {
        return new Point(
            canvasPoint.X * Zoom + PanX,
            canvasPoint.Y * Zoom + PanY
        );
    }

    internal void Copy(out GraphSnapshot? clipboard)
    {
        if (SelectedNodes.Count == 0)
        {
            clipboard = null;
            return;
        }

        var selectedIds = SelectedNodes.Where(nodeVm =>
                nodeVm.NodeLogic.GetType() != typeof(ArgumentsNode) && nodeVm.NodeLogic.GetType() != typeof(ReturnNode))
            .Select(n => n.Id)
            .ToHashSet();
        var tempGraph = new NodeGraph();

        foreach (var id in selectedIds)
        {
            var node = _graph.GetNode(id);
            if (node != null)
            {
                tempGraph.AddNode(node);
                if (_graph.VisualStates.TryGetValue(id, out var visualState)) tempGraph.VisualStates[id] = visualState;
            }
        }

        foreach (var conn in _graph.Connections)
            if (selectedIds.Contains(conn.FromId) && selectedIds.Contains(conn.ToId))
                tempGraph.Connections.Add(conn);

        clipboard = Serializer.Create(tempGraph);
        CommandManager.InvalidateRequerySuggested();
    }

    internal void Paste(GraphSnapshot? clipboard)
    {
        if (clipboard == null)
            return;

        var tempGraph = Serializer.Restore(clipboard);

        foreach (var kv in tempGraph.Nodes.ToList())
            if (kv.Value.GetType() == typeof(ArgumentsNode) || kv.Value.GetType() == typeof(ReturnNode))
                tempGraph.RemoveNode(kv.Key);

        var nodes = tempGraph.Nodes.Values.ToList();
        if (nodes.Count == 0)
            return;

        var positions = nodes.Select(n =>
        {
            if (tempGraph.VisualStates.TryGetValue(n.Id, out var vs))
                return new Point(vs.X, vs.Y);

            return new Point(0, 0);
        }).ToList();

        var minX = positions.Min(p => p.X);
        var minY = positions.Min(p => p.Y);

        var anchor = PendingContextPoint ?? new Point(minX + 50, minY + 50);
        var dx = anchor.X - minX;
        var dy = anchor.Y - minY;

        var idMapping = new Dictionary<Guid, Guid>();

        foreach (var node in nodes)
        {
            var oldId = node.Id;
            var newId = Guid.NewGuid();

            idMapping[oldId] = newId;
            node.Id = newId;

            _graph.AddNode(node);

            if (tempGraph.VisualStates.TryGetValue(oldId, out var oldVisualState))
                _graph.SetVisualState(newId, oldVisualState.X + dx, oldVisualState.Y + dy);
        }

        foreach (var conn in tempGraph.Connections)
            if (idMapping.TryGetValue(conn.FromId, out var newFromId) &&
                idMapping.TryGetValue(conn.ToId, out var newToId))
                _graph.Connect(newFromId, conn.FromPort, newToId, conn.ToPort);

        _graph.Commit();
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