using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class NodeViewModel : INotifyPropertyChanged
{
    private readonly NodeGraph _graph;
    internal readonly NodeLogic NodeLogic;

    private double _x;
    private double _y;

    public NodeViewModel(NodeLogic nodeLogic, NodeGraph graph, NodeEditorViewModel nodeEditorViewModel)
    {
        NodeLogic = nodeLogic;
        _graph = graph;
        ParentEditor = nodeEditorViewModel;

        Id = nodeLogic.Id;

        var nodeAttr = nodeLogic.GetType().GetCustomAttribute<NodeAttribute>();
        if (nodeAttr == null) throw new InvalidOperationException();
        DisplayName = nodeAttr.Label;
        var instance = (INodeCategory)Activator.CreateInstance(nodeAttr.CategoryType)!;
        Category = instance.Category;
        Description = nodeAttr.Description;
        Color = instance.Color;

        InputPorts = new ObservableCollection<PortViewModel>(
            CreateInputPorts(nodeLogic, graph)
        );
        OutputPorts = new ObservableCollection<PortViewModel>(
            CreateOutputPorts(nodeLogic, graph)
        );
        SubGraphs = new ObservableCollection<SubGraphViewModel>(
            CreateSubGraphs(nodeLogic)
        );

        HasSubGraph = nodeLogic.SubGraphs.Count > 0;

        var visualState = graph.GetVisualState(Id);
        _x = visualState?.X ?? 0;
        _y = visualState?.Y ?? 0;
    }

    public NodeEditorViewModel ParentEditor { get; }

    public bool IsSelected
    {
        get;
        internal set => SetField(ref field, value);
    }

    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }
    public string Color { get; }

    public Guid Id { get; }

    public ObservableCollection<PortViewModel> InputPorts { get; }
    public ObservableCollection<PortViewModel> OutputPorts { get; }
    public ObservableCollection<SubGraphViewModel> SubGraphs { get; }

    public bool HasSubGraph { get; }

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
            var portColorAttr = prop?.GetCustomAttribute<PortColorSettingAttribute>();
            var controlAttr = prop?.GetCustomAttribute<PropertyControlBaseAttribute>();

            yield return new PortViewModel(
                name,
                portAttr?.Label ?? name,
                portAttr?.Description ?? "",
                portColorAttr?.Color ?? nameof(Colors.SlateGray),
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
            var portColorAttr = prop?.GetCustomAttribute<PortColorSettingAttribute>();

            yield return new PortViewModel(
                name,
                portAttr?.Label ?? name,
                portAttr?.Description ?? "",
                portColorAttr?.Color ?? nameof(Colors.SlateGray),
                port.ValueType,
                PortDirection.Output,
                null,
                graph,
                node.Id
            );
        }
    }

    private IEnumerable<SubGraphViewModel> CreateSubGraphs(NodeLogic node)
    {
        foreach (var (name, subGraph) in node.SubGraphs)
        {
            var prop = node.GetType().GetProperty(name);
            var portAttr = prop?.GetCustomAttribute<SubGraphAttribute>();

            yield return new SubGraphViewModel(
                name,
                portAttr?.Label ?? name,
                portAttr?.Description ?? "",
                subGraph
            )
            {
                OpenSubGraphCommand = new RelayCommand(() => { ParentEditor.OpenGraph(subGraph, name); }),
                OnGraphChangedCommand = new RelayCommand<GraphChangedEventArgs>(args =>
                {
                    if (args != null) _graph.OnGraphChanged(args);
                }),
                OnGraphCommitedCommand = new RelayCommand(() => _graph.Commit())
            };
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