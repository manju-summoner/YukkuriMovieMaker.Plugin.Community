namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public sealed class NodeGraph
{
    private readonly Dictionary<Guid, NodeLogic> _nodes = new();
    public readonly List<NodeConnection> Connections = [];
    public readonly Dictionary<Guid, NodeVisualState> VisualStates = new();
    public IReadOnlyDictionary<Guid, NodeLogic> Nodes => _nodes;

    public void AddNode(NodeLogic node)
    {
        _nodes[node.Id] = node;
    }

    public NodeLogic? GetNode(Guid id)
    {
        return _nodes.GetValueOrDefault(id);
    }

    public void SetVisualState(Guid id, double x, double y)
    {
        var visual = new NodeVisualState { Id = id, X = x, Y = y };
        VisualStates[id] = visual;
    }

    public NodeVisualState? GetVisualState(Guid id)
    {
        VisualStates.TryGetValue(id, out var visualState);
        return visualState;
    }

    public async Task<object?> GetOutputValue(Guid id, string outputName)
    {
        if (!_nodes.TryGetValue(id, out var node)) return null;
        return await node.Outputs[outputName].GetValue();
    }

    public void Connect(
        Guid from, string outputName,
        Guid to, string inputName)
    {
        var output = _nodes[from].Outputs[outputName];
        var input = _nodes[to].Inputs[inputName];
        input.Connect(output);
        Connections.Add(
            new NodeConnection { FromId = from, FromPort = outputName, ToId = to, ToPort = inputName });
    }

    public void Disconnect(
        Guid from, string outputName,
        Guid to, string inputName)
    {
        var output = _nodes[from].Outputs[outputName];
        var input = _nodes[to].Inputs[inputName];
        input.DisConnect(output);
        Connections.Remove(
            new NodeConnection { FromId = from, FromPort = outputName, ToId = to, ToPort = inputName });
    }

    public void SetInputValue(Guid id, string inputName, object? value)
    {
        var input = _nodes[id].Inputs[inputName];
        input.SetValue(value);
    }
}