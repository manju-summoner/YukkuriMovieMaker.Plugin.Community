namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

public static class Serializer
{
    public static GraphSnapshot Create(NodeGraph graph)
    {
        var snapshot = new GraphSnapshot();

        foreach (var node in graph.Nodes.Values)
        {
            var snap = new NodeSnapshot
            {
                Id = node.Id,
                TypeName = node.GetType().AssemblyQualifiedName ?? node.GetType().Name,
                X = graph.GetVisualState(node.Id)?.X ?? 0,
                Y = graph.GetVisualState(node.Id)?.Y ?? 0
            };

            foreach (var input in node.Inputs) snap.InputsValues[input.Key] = input.Value.GetValue();

            snapshot.Nodes.Add(snap);
        }

        snapshot.Connections.AddRange(
            graph.Connections.Select(c => new ConnectionSnapshot
            {
                FromId = c.FromId,
                ToId = c.ToId,
                FromPort = c.FromPort,
                ToPort = c.ToPort
            }));

        return snapshot;
    }

    public static NodeGraph Restore(GraphSnapshot snapshot)
    {
        var graph = new NodeGraph();

        foreach (var nodeSnap in snapshot.Nodes)
        {
            var type = Type.GetType(nodeSnap.TypeName);
            if (type == null) continue;
            var node = (NodeLogic)Activator.CreateInstance(type)!;
            node.Id = nodeSnap.Id;

            graph.AddNode(node);
            graph.SetVisualState(node.Id, nodeSnap.X, nodeSnap.Y);

            foreach (var input in nodeSnap.InputsValues) graph.SetInputValue(node.Id, input.Key, input.Value);
        }

        foreach (var conn in snapshot.Connections)
            graph.Connect(
                conn.FromId, conn.FromPort,
                conn.ToId, conn.ToPort);

        return graph;
    }
}