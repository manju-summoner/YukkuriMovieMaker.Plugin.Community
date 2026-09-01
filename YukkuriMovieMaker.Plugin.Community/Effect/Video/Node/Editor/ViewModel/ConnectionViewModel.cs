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
            FromPort = fromNode.OutputPorts.FirstOrDefault(p => p.Name == FromPortName);

        if (nodeViewModels.TryGetValue(ToNodeId, out var toNode))
        {
            ToPort = toNode.InputPorts.FirstOrDefault(p => p.Name == ToPortName);
            if (ToPort != null)
                ToPort.IsConnected = true;
        }

        if (FromPort != null)
            FromPort.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortViewModel.Position))
                {
                    OnPropertyChanged(nameof(Geometry));
                    OnPropertyChanged(nameof(Brush));
                }
            };

        if (ToPort != null)
            ToPort.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortViewModel.Position))
                {
                    OnPropertyChanged(nameof(Geometry));
                    OnPropertyChanged(nameof(Brush));
                }
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
            if (FromPort is null || ToPort is null)
                return new SolidColorBrush(Colors.SlateGray);

            var reverseX = ToPort.Position.X < FromPort.Position.X;
            var reverseY = ToPort.Position.Y < FromPort.Position.Y;
            var brush = new LinearGradientBrush
            {
                StartPoint = reverseX ? reverseY ? new Point(1, 1) : new Point(1, 0)
                    : reverseY ? new Point(0, 1) : new Point(0, 0),
                EndPoint = reverseX ? reverseY ? new Point(0, 1) : new Point(0, 0)
                    : reverseY ? new Point(1, 1) : new Point(1, 0)
            };

            var fromColor = ResolveColor(FromPort.Color);
            var toColor = ResolveColor(ToPort.Color);
            brush.GradientStops.Add(new GradientStop(fromColor, 0.4));
            brush.GradientStops.Add(new GradientStop(toColor, 0.6));
            return brush;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static Color ResolveColor(string colorName)
    {
        try
        {
            if (colorName.StartsWith('#'))
                return (Color)ColorConverter.ConvertFromString(colorName)!;

            if (typeof(Colors).GetProperty(colorName)?.GetValue(null) is Color color)
                return color;
        }
        catch
        {
            // 未知の色名・不正なフォーマットは既定色にフォールバックする。
        }

        return Colors.SlateGray;
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