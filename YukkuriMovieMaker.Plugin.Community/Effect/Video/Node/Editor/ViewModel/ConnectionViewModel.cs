using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    }

    public Guid FromNodeId { get; }
    public string FromPortName { get; }
    public Guid ToNodeId { get; }
    public string ToPortName { get; }

    public PortViewModel? FromPort { get; }
    public PortViewModel? ToPort { get; }

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