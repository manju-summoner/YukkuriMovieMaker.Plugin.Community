using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class PortViewModel : INotifyPropertyChanged
{
    private readonly NodeGraph _graph;
    internal readonly Guid NodeId;

    private object? _currentValue;

    public PortViewModel(
        string name,
        string label,
        string description,
        Type valueType,
        PortDirection direction,
        PropertyControlBaseAttribute? controlAttribute,
        NodeGraph graph,
        Guid nodeId)
    {
        Name = name;
        Label = label;
        Description = description;
        ValueType = valueType;
        Direction = direction;
        ControlAttribute = controlAttribute;
        _graph = graph;
        NodeId = nodeId;

        // 初期値を取得
        if (direction == PortDirection.Input)
        {
            var port = graph.Nodes[nodeId].Inputs[name];
            _currentValue = port.GetValue().GetAwaiter().GetResult();
        }
    }

    public string Name { get; }
    public string Label { get; }
    public string Description { get; }
    public Type ValueType { get; }
    public PortDirection Direction { get; }

    public object? CurrentValue
    {
        get => _currentValue;
        set
        {
            if (_currentValue != value)
            {
                _currentValue = value;
                OnPropertyChanged();

                if (Direction == PortDirection.Input) _graph.SetInputValue(NodeId, Name, value);
            }
        }
    }

    public PropertyControlBaseAttribute? ControlAttribute { get; }
    public bool HasControl => ControlAttribute != null && Direction == PortDirection.Input;

    public bool IsConnected
    {
        get;
        set => SetField(ref field, value);
    }

    public Point Position
    {
        get;
        set => SetField(ref field, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateValueFromGraph(object? value)
    {
        if (_currentValue != value)
        {
            _currentValue = value;
            OnPropertyChanged(nameof(CurrentValue));
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

public enum PortDirection
{
    Input,
    Output
}