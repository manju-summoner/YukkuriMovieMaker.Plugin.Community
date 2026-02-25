using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class ConnectionViewModel : INotifyPropertyChanged
{
    public ConnectionViewModel(
        NodeConnection connection,
        Dictionary<Guid, NodeViewModel> nodeViewModels)
    {
        FromNodeId = connection.FromId;
        FromPortName = connection.FromPort;
        ToNodeId = connection.ToId;
        ToPortName = connection.ToPort;

        if (nodeViewModels.TryGetValue(FromNodeId, out var fromNode))
            FromPort = fromNode.OutputPorts.First(p => p.Name == FromPortName);

        if (nodeViewModels.TryGetValue(ToNodeId, out var toNode))
        {
            ToPort = toNode.InputPorts.First(p => p.Name == ToPortName);
            ToPort.IsConnected = true;
        }

        if (FromPort != null)
            FromPort.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortViewModel.Position))
                    OnPropertyChanged(nameof(Geometry));
            };

        if (ToPort != null)
            ToPort.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortViewModel.Position))
                    OnPropertyChanged(nameof(Geometry));
            };
    }

    public Guid FromNodeId { get; }
    public string FromPortName { get; }
    public Guid ToNodeId { get; }
    public string ToPortName { get; }

    public PortViewModel? FromPort { get; }
    public PortViewModel? ToPort { get; }

    public Geometry Geometry
    {
        get
        {
            var start = FromPort?.Position ?? default;
            var end = ToPort?.Position ?? default;

            var dx = Math.Abs(end.X - start.X) * 0.5;

            var c1 = start with { X = start.X + dx };
            var c2 = end with { X = end.X - dx };

            var fig = new PathFigure { StartPoint = start };
            fig.Segments.Add(new BezierSegment(c1, c2, end, true));

            return new PathGeometry([fig]);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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