using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public class GraphViewModel : INotifyPropertyChanged
{
    private readonly NodeGraph _graph;

    public GraphViewModel(NodeGraph graph)
    {
        _graph = graph;

        _graph.GraphChanged += OnGraphChanged;

        SyncFromGraph();

        AddNodeCommand = new RelayCommand<Type>(AddNode);
        DeleteNodeCommand = new RelayCommand<Guid>(DeleteNode);
        ConnectPortsCommand = new RelayCommand<(PortViewModel From, PortViewModel To)>(ConnectPorts);
        DisconnectCommand = new RelayCommand<ConnectionViewModel>(Disconnect);
    }

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    public ICommand AddNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand ConnectPortsCommand { get; }
    public ICommand DisconnectCommand { get; }

    public NodeViewModel? SelectedNode { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void UpdatePortValue(ValueChangedEventArgs e)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == e.NodeId);
        if (node == null) return;

        var port = node.InputPorts.FirstOrDefault(p => p.Name == e.PortName);
        if (port != null)
            // CurrentValue の setter を経由せずに直接更新（無限ループ回避）
            port.UpdateValueFromGraph(e.NewValue);
    }

    private void SyncFromGraph()
    {
        var nodeViewModels = new Dictionary<Guid, NodeViewModel>();

        Nodes.Clear();
        foreach (var node in _graph.Nodes.Values)
        {
            var vm = new NodeViewModel(node, _graph);
            Nodes.Add(vm);
            nodeViewModels[node.Id] = vm;
        }

        Connections.Clear();
        foreach (var conn in _graph.Connections)
        {
            var vm = new ConnectionViewModel(conn, nodeViewModels);
            Connections.Add(vm);
        }
    }

    private void OnGraphChanged(object? sender, GraphChangedEventArgs e)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            switch (e)
            {
                case NodeAddedEventArgs added:
                    var vm = new NodeViewModel(added.Node, _graph);
                    Nodes.Add(vm);
                    break;

                case NodeRemovedEventArgs removed:
                    var node = Nodes.FirstOrDefault(n => n.Id == removed.NodeId);
                    if (node != null) Nodes.Remove(node);
                    break;

                case ConnectionChangedEventArgs connChanged:
                    break;

                case ValueChangedEventArgs valueChanged:
                    UpdatePortValue(valueChanged);
                    break;
            }
        });
    }

    private void AddNode(Type nodeType)
    {
        if (nodeType == null!) return;

        _graph.BeginEdit();

        var node = (NodeLogic)Activator.CreateInstance(nodeType)!;
        node.Id = Guid.NewGuid();

        _graph.AddNode(node);
        _graph.SetVisualState(node.Id, 100, 100);

        _graph.EndEdit();
    }

    private void DeleteNode(Guid nodeId)
    {
        _graph.BeginEdit();
        _graph.RemoveNode(nodeId);
        _graph.EndEdit();
    }

    private void ConnectPorts((PortViewModel From, PortViewModel To) ports)
    {
        if (ports.From.Direction != PortDirection.Output ||
            ports.To.Direction != PortDirection.Input)
            return;

        _graph.BeginEdit();
        _graph.Connect(
            ports.From._nodeId, ports.From.Name,
            ports.To._nodeId, ports.To.Name
        );
        _graph.EndEdit();
    }

    private void Disconnect(ConnectionViewModel connection)
    {
        _graph.BeginEdit();
        _graph.Disconnect(
            connection.FromNodeId, connection.FromPortName,
            connection.ToNodeId, connection.ToPortName
        );
        _graph.EndEdit();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}