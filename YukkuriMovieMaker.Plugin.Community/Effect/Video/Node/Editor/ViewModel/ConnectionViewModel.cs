using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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

    public System.Windows.Media.Brush Brush
    {
        get
        {
            if (FromPort is null || ToPort is null) return new SolidColorBrush(Colors.SlateGray);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                MappingMode = BrushMappingMode.Absolute
            };
            var fromColor = (Color)(FromPort.Color.StartsWith('#')
                ? ColorConverter.ConvertFromString(FromPort.Color)
                : typeof(Colors).GetProperty(FromPort.Color)?.GetValue(null) ??
                  throw new InvalidOperationException($"Unknown color: {FromPort.Color}"));
            var toColor = (Color)(ToPort.Color.StartsWith('#')
                ? ColorConverter.ConvertFromString(ToPort.Color)
                : typeof(Colors).GetProperty(ToPort.Color)?.GetValue(null) ??
                  throw new InvalidOperationException($"Unknown color: {ToPort.Color}"));
            brush.GradientStops.Add(new GradientStop(fromColor, 0.2));
            brush.GradientStops.Add(new GradientStop(toColor, 0.8));
            return brush;
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