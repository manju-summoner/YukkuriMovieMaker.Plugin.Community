using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
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
        string color,
        Type valueType,
        PortDirection direction,
        PropertyControlBaseAttribute? controlAttribute,
        NodeGraph graph,
        Guid nodeId)
    {
        Name = name;
        Label = label;
        Description = description;
        Color = color;
        ValueType = valueType;
        Direction = direction;
        ControlAttribute = controlAttribute;
        _graph = graph;
        NodeId = nodeId;

        if (direction == PortDirection.Input)
        {
            var port = graph.Nodes[nodeId].Inputs[name];
            _currentValue = port.LocalValue;

            if (_currentValue == null && controlAttribute != null)
                _currentValue = controlAttribute.GetDefaultValue();
        }

        BeginEditCommand = new RelayCommand(() => _graph.BeginEdit());
        EndEditCommand = new RelayCommand(() => _graph.EndEdit());
    }

    public string Name { get; }
    public string Label { get; }
    public string Description { get; }
    public string Color { get; }
    public Type ValueType { get; }
    public PortDirection Direction { get; }

    public object? CurrentValue
    {
        get => _currentValue;
        set
        {
            if (!Equals(_currentValue, value))
            {
                _currentValue = value;
                OnPropertyChanged();

                if (Direction == PortDirection.Input)
                {
                    _graph.SetInputValue(NodeId, Name, value);
                }
            }
        }
    }

    public PropertyControlBaseAttribute? ControlAttribute { get; }
    public bool HasControl => ControlAttribute != null && Direction == PortDirection.Input;

    public ICommand BeginEditCommand { get; }
    public ICommand EndEditCommand { get; }

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
        if (!Equals(_currentValue, value))
        {
            _currentValue = value;
            OnPropertyChanged(nameof(CurrentValue));
        }
    }

    internal void ApplyDefaultToGraph()
    {
        if (Direction != PortDirection.Input) return;
        var port = _graph.Nodes[NodeId].Inputs[Name];
        if (port.LocalValue != null) return;
        if (ControlAttribute == null) return;
        var defaultValue = ControlAttribute.GetDefaultValue();
        if (defaultValue == null) return;
        _graph.SetInputValue(NodeId, Name, defaultValue);
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