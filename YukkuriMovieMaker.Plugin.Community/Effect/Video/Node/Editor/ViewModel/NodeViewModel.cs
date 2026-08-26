using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class NodeViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly EventHandler _errorStateChangedHandler;
    private readonly NodeGraph _graph;
    private readonly GraphViewModel _graphViewModel;
    private readonly EventHandler<NeedToReinitializeInputPortsEvent> _needToReinitializeInputPortsHandler;
    internal readonly NodeLogic NodeLogic;
    private bool _isDisposed;

    private double _x;
    private double _y;

    public NodeViewModel(NodeLogic nodeLogic, NodeGraph graph, GraphViewModel graphViewModel)
    {
        NodeLogic = nodeLogic;
        _graph = graph;
        _graphViewModel = graphViewModel;
        ParentEditor = graphViewModel.ParentEditor;

        Id = nodeLogic.Id;

        var nodeAttr = nodeLogic.GetType().GetCustomAttribute<NodeAttribute>();
        if (nodeAttr == null) throw new InvalidOperationException();
        DisplayName = nodeAttr.GetLabel();
        Category = nodeAttr.GetCategoryName();
        Description = nodeAttr.GetDescription();
        Color = nodeAttr.GetCategoryColor();

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

        IsError = nodeLogic.HasError;
        _errorStateChangedHandler = (sender, _) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => IsError = ((NodeLogic)sender!).HasError);
                return;
            }

            IsError = ((NodeLogic)sender!).HasError;
        };
        nodeLogic.ErrorStateChanged += _errorStateChangedHandler;

        _needToReinitializeInputPortsHandler = (sender, @event) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => nodeLogic.InvokeNeedToReinitializeInputPorts(sender, @event));
                return;
            }

            var logic = (NodeLogic)sender!;

            var savedConnections = InputPorts
                .Select(portVm => new
                {
                    portVm.Name,
                    ControlAttributeType = portVm.ControlAttribute?.ControlType,
                    EditorAttributeType = portVm.EditorAttribute?.GetType(),
                    Connections = graph.Connections
                        .Where(c => c.ToId == Id && c.ToPort == portVm.Name)
                        .Select(c => new { c.FromId, c.FromPort })
                        .ToList(),
                    LocalValue = logic.Inputs.TryGetValue(portVm.Name, out var oldPort)
                        ? oldPort.LocalValue
                        : null
                }).ToList();

            var prefix = @event.PropName + ".";
            var newSubPortNames = @event.NewContainer.GetType()
                .GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(InputPortAttribute)))
                .Select(p => prefix + p.Name)
                .ToHashSet();

            foreach (var port in savedConnections)
            {
                if (!port.Name.StartsWith(prefix)) continue;
                if (newSubPortNames.Contains(port.Name)) continue;
                foreach (var conn in port.Connections)
                    graph.DisconnectSilently(conn.FromId, conn.FromPort, Id, port.Name);
            }

            logic.SwapDynamicContainer(@event.PropName, @event.NewContainer);

            InputPorts.Clear();
            foreach (var portVm in CreateInputPorts(logic, graph))
                InputPorts.Add(portVm);

            var newPortsByName = InputPorts.ToDictionary(p => p.Name);
            foreach (var saved in savedConnections)
            {
                if (!newPortsByName.TryGetValue(saved.Name, out var newPortVm)) continue;
                if (newPortVm.ControlAttribute?.ControlType != saved.ControlAttributeType) continue;
                if (newPortVm.EditorAttribute?.GetType() != saved.EditorAttributeType) continue;

                if (saved.Connections.Count > 0)
                {
                    foreach (var conn in saved.Connections)
                        graph.Connect(conn.FromId, conn.FromPort, Id, saved.Name);
                }
                else if (saved.Name.StartsWith(prefix))
                {
                    var subName = saved.Name[prefix.Length..];
                    var restoreValue = saved.LocalValue
                                       ?? @event.NewContainer.GetType().GetProperty(subName)
                                           ?.GetValue(@event.NewContainer);
                    graph.SetInputValue(Id, saved.Name, restoreValue);
                }
            }

            graph.OnGraphChanged(new ConnectionChangedEventArgs(null, null, Id, null));
        };
        nodeLogic.NeedToReinitializeInputPorts += _needToReinitializeInputPortsHandler;
    }

    public NodeEditorViewModel ParentEditor { get; }

    public bool IsSelected
    {
        get;
        internal set => SetField(ref field, value);
    }

    public bool IsError
    {
        get;
        private set => SetField(ref field, value);
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        NodeLogic.ErrorStateChanged -= _errorStateChangedHandler;
        NodeLogic.NeedToReinitializeInputPorts -= _needToReinitializeInputPortsHandler;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void CommitPosition()
    {
        _graph.SetVisualState(Id, _x, _y);
    }

    private IEnumerable<PortViewModel> CreateInputPorts(NodeLogic node, NodeGraph graph)
    {
        var orderedKeys = node.Inputs.Keys.OrderBy(name =>
        {
            if (!name.Contains('.')) return (0, 0);
            var parts = name.Split('.', 2);
            var containerProp = node.GetType().GetProperty(parts[0]);
            if (containerProp == null) return (0, 0);
            var containerInstance = containerProp.GetValue(node);
            var subProp = containerInstance?.GetType().GetProperty(parts[1]);
            return (containerProp.MetadataToken, subProp?.MetadataToken ?? 0);
        });

        foreach (var name in orderedKeys)
        {
            var port = node.Inputs[name];
            PropertyInfo? prop;
            object? editorPropertyOwner;
            if (name.Contains('.'))
            {
                var parts = name.Split('.', 2);
                var containerProp = node.GetType().GetProperty(parts[0]);
                var containerInstance = containerProp?.GetValue(node);
                prop = containerInstance?.GetType().GetProperty(parts[1]);
                editorPropertyOwner = containerInstance;
            }
            else
            {
                prop = node.GetType().GetProperty(name);
                editorPropertyOwner = node;
            }

            var portAttr = prop?.GetCustomAttribute<InputPortAttribute>();
            var portColorAttr = prop?.GetCustomAttribute<PortColorSettingAttribute>();
            var controlAttr = prop?.GetCustomAttribute<PropertyControlBaseAttribute>();
            var editorAttr = controlAttr == null ? ResolveEditorAttribute(prop) : null;

            yield return new PortViewModel(
                name,
                portAttr?.GetLabel() ?? name,
                portAttr?.GetDescription() ?? "",
                portColorAttr?.Color ?? nameof(Colors.SlateGray),
                port.ValueType,
                PortDirection.Input,
                controlAttr,
                editorAttr,
                editorPropertyOwner,
                prop,
                _graphViewModel.EditorInfo,
                graph,
                node.Id
            );
        }
    }

    /// <summary>
    ///     プロパティに付与された PropertyEditorAttribute2 継承属性を解決する。
    ///     通常は GetCustomAttribute で十分だが、Reflection.Emit で動的生成された型
    ///     （EffectNodeFactory / BrushNodeFactory / ContainerFactory が生成する型）の場合、
    ///     元の属性が「別アセンブリの internal 型」（YMM4本体組み込みのエディタ等、こちらが
    ///     InternalsVisibleTo を付与できない相手）だと、CustomAttributeBuilder で複製した属性を
    ///     GetCustomAttribute で再インスタンス化する際にアクセス例外で失敗することがある。
    ///     そのため、動的生成側でビルド時に直接インスタンス化して静的フィールド（_portDefs）に
    ///     保持してある PropertyEditorAttribute2 インスタンスがあれば、そちらを優先して使う。
    ///     手書きの NodeLogic クラス（通常のコンパイル時属性）には _portDefs が存在しないため、
    ///     その場合は今までどおり GetCustomAttribute で解決する。
    /// </summary>
    private static PropertyEditorAttribute2? ResolveEditorAttribute(PropertyInfo? prop)
    {
        if (prop == null) return null;

        var portDefsField = prop.DeclaringType?.GetField("_portDefs", BindingFlags.NonPublic | BindingFlags.Static);
        if (portDefsField?.GetValue(null) is PortDefinition[] portDefs)
        {
            var match = portDefs.FirstOrDefault(d => d.PropName == prop.Name);
            if (match?.EditorAttributeInstance != null)
                return match.EditorAttributeInstance;
        }

        return prop.GetCustomAttribute<PropertyEditorAttribute2>();
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
                portAttr?.GetLabel() ?? name,
                portAttr?.GetDescription() ?? "",
                portColorAttr?.Color ?? nameof(Colors.SlateGray),
                port.ValueType,
                PortDirection.Output,
                null,
                null,
                null,
                null,
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
                portAttr?.GetLabel() ?? name,
                portAttr?.GetDescription() ?? "",
                subGraph
            )
            {
                OpenSubGraphCommand = new RelayCommand(() =>
                {
                    ParentEditor.OpenGraph(subGraph, name, _graphViewModel.EditorInfo);
                }),
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