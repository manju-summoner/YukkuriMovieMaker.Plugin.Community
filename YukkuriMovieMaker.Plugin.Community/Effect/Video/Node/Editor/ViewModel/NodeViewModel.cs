using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class NodeViewModel : INotifyPropertyChanged
{
    private readonly NodeGraph _graph;
    private readonly NodeLogic _nodeLogic;

    private double _x;
    private double _y;

    public NodeViewModel(NodeLogic nodeLogic, NodeGraph graph)
    {
        _nodeLogic = nodeLogic;
        _graph = graph;

        Id = nodeLogic.Id;

        var nodeAttr = nodeLogic.GetType().GetCustomAttribute<NodeAttribute>();
        DisplayName = nodeAttr?.Label ?? nodeLogic.GetType().Name;
        Category = nodeAttr?.Category ?? "Misc";
        Description = nodeAttr?.Description ?? "";

        InputPorts = new ObservableCollection<PortViewModel>(
            CreateInputPorts(nodeLogic, graph)
        );
        OutputPorts = new ObservableCollection<PortViewModel>(
            CreateOutputPorts(nodeLogic, graph)
        );

        HasSubGraph = nodeLogic.SubGraphs.Count > 0;

        var visualState = graph.GetVisualState(Id);
        _x = visualState?.X ?? 0;
        _y = visualState?.Y ?? 0;
    }

    public bool IsSelected
    {
        get;
        internal set => SetField(ref field, value);
    }

    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }

    public Guid Id { get; }

    public ObservableCollection<PortViewModel> InputPorts { get; }
    public ObservableCollection<PortViewModel> OutputPorts { get; }

    public bool HasSubGraph { get; }
    public ICommand? OpenSubGraphCommand { get; }

    public double X
    {
        get => _x;
        set
        {
            if (Math.Abs(_x - value) > 0.0001)
            {
                _x = value;
                OnPropertyChanged();
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) > 0.0001)
            {
                _y = value;
                OnPropertyChanged();
            }
        }
    }

    public double Width
    {
        get;
        internal set => SetField(ref field, value);
    }

    public double Height
    {
        get;
        internal set => SetField(ref field, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void CommitPosition()
    {
        _graph.SetVisualState(Id, _x, _y);
    }

    private IEnumerable<PortViewModel> CreateInputPorts(NodeLogic node, NodeGraph graph)
    {
        foreach (var (name, port) in node.Inputs)
        {
            var prop = node.GetType().GetProperty(name);
            var portAttr = prop?.GetCustomAttribute<InputPortAttribute>();
            var controlAttr = prop?.GetCustomAttribute<PropertyControlBaseAttribute>();

            yield return new PortViewModel(
                name,
                portAttr?.Label ?? name,
                portAttr?.Description ?? "",
                port.ValueType,
                PortDirection.Input,
                controlAttr,
                graph,
                node.Id
            );
        }
    }

    private IEnumerable<PortViewModel> CreateOutputPorts(NodeLogic node, NodeGraph graph)
    {
        foreach (var (name, port) in node.Outputs)
        {
            var prop = node.GetType().GetProperty(name);
            var portAttr = prop?.GetCustomAttribute<OutputPortAttribute>();

            yield return new PortViewModel(
                name,
                portAttr?.Label ?? name,
                portAttr?.Description ?? "",
                port.ValueType,
                PortDirection.Output,
                null,
                graph,
                node.Id
            );
        }
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