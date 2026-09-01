namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;

public class GraphChangedEventArgs : EventArgs
{
    public DateTime Timestamp { get; } = DateTime.Now;

    public bool IsInTransaction { get; internal set; }
}

public class NodeAddedEventArgs : GraphChangedEventArgs
{
    public NodeAddedEventArgs(Guid nodeId, NodeLogic node)
    {
        NodeId = nodeId;
        Node = node;
    }

    public Guid NodeId { get; }
    public NodeLogic Node { get; }
}

public class NodeRemovedEventArgs : GraphChangedEventArgs
{
    public NodeRemovedEventArgs(Guid nodeId)
    {
        NodeId = nodeId;
    }

    public Guid NodeId { get; }
}

public class VisualStateChangedEventArgs : GraphChangedEventArgs
{
    public VisualStateChangedEventArgs(Guid nodeId, NodeVisualState newState)
    {
        NodeId = nodeId;
        NewState = newState;
    }

    public Guid NodeId { get; }
    public NodeVisualState NewState { get; }
}

public class ValueChangedEventArgs : GraphChangedEventArgs
{
    public ValueChangedEventArgs(Guid nodeId, string portName, object? newValue)
    {
        NodeId = nodeId;
        PortName = portName;
        NewValue = newValue;
    }

    public Guid NodeId { get; }
    public string PortName { get; }
    public object? NewValue { get; }
}

public class ConnectionChangedEventArgs : GraphChangedEventArgs
{
    public ConnectionChangedEventArgs(Guid? fromNodeId, string? fromPortName, Guid? toNodeId, string? toPortName)
    {
        FromNodeId = fromNodeId;
        FromPortName = fromPortName;
        ToNodeId = toNodeId;
        ToPortName = toPortName;
    }

    public Guid? FromNodeId { get; }
    public string? FromPortName { get; }
    public Guid? ToNodeId { get; }
    public string? ToPortName { get; }
}

public class CommittedEventArgs : EventArgs
{
    public DateTime Timestamp { get; } = DateTime.Now;
}