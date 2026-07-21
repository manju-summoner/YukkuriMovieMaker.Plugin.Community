using System.Reflection;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Brush;
using PortDefinition = YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port.PortDefinition;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

public static class Serializer
{
    public static GraphSnapshot Create(NodeGraph graph)
    {
        return CreateAsync(graph).GetAwaiter().GetResult();
    }

    public static async Task<GraphSnapshot> CreateAsync(NodeGraph graph)
    {
        var snapshot = new GraphSnapshot();

        foreach (var node in graph.Nodes.Values)
        {
            node.UpdateSubGraphs();

            var snap = new NodeSnapshot
            {
                Id = node.Id,
                TypeName = node.GetType().AssemblyQualifiedName ?? node.GetType().Name,
                X = graph.GetVisualState(node.Id)?.X ?? 0,
                Y = graph.GetVisualState(node.Id)?.Y ?? 0,
                CustomData = (node as ISerializableNode)?.SerializeCustomData() ?? new Dictionary<string, object?>()
            };

            foreach (var input in node.Inputs)
                if (!input.Value.IsConnected)
                    snap.InputsValues[input.Key] = await input.Value.GetValue();
            foreach (var subGraph in node.SubGraphs) snap.SubGraphs[subGraph.Key] = await CreateAsync(subGraph.Value);

            switch (node)
            {
                case ArgumentsNode argsNode:
                    SerializePortDefinitions(snap, argsNode.GetPortDefinitions());
                    break;
                case ReturnNode retNode:
                    SerializePortDefinitions(snap, retNode.GetPortDefinitions());
                    break;
            }

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

        foreach (var nodeSnap in snapshot.Nodes)
        {
            var typeName = nodeSnap.TypeName;
            var effectName = EffectNodeFactory.GetEffectName(typeName);
            if (effectName != null)
                snapshot.EffectTypeNames[typeName] = effectName;
            var brushName = DynamicBrushNodeFactory.GetBrushName(typeName);
            if (brushName != null)
                snapshot.BrushTypeNames[typeName] = brushName;
        }

        return snapshot;

        void SerializePortDefinitions(NodeSnapshot snap, PortDefinition[] portDefs)
        {
            foreach (var portDef in portDefs)
                snap.PortDefinitions[portDef.Name] = new PortDefinitionSnapshot
                {
                    Name = portDef.Name,
                    TypeName = portDef.ValueType.AssemblyQualifiedName ?? portDef.ValueType.Name,
                    Label = portDef.Label,
                    Description = portDef.Description,
                    DefaultValue = portDef.DefaultValue
                };
        }
    }

    public static NodeGraph Restore(GraphSnapshot snapshot)
    {
        var graph = new NodeGraph();
        var nodeMap = new Dictionary<Guid, NodeLogic>();

        foreach (var (_, effectName) in snapshot.EffectTypeNames)
        {
            var nodeType = EffectNodeFactory.GetOrCreate(effectName);
            if (nodeType != null)
                Registry.RegisterType(nodeType);
        }

        foreach (var (_, brushName) in snapshot.BrushTypeNames)
        {
            var nodeType = DynamicBrushNodeFactory.GetOrCreate(brushName);
            if (nodeType != null)
                Registry.RegisterType(nodeType);
        }

        foreach (var nodeSnap in snapshot.Nodes)
        {
            var type = Registry.AllNodes.GetValueOrDefault(nodeSnap.TypeName)
                       ?? Type.GetType(nodeSnap.TypeName);
            if (type == null) continue;
            var node = (NodeLogic)Activator.CreateInstance(type)!;
            node.Id = nodeSnap.Id;

            nodeMap[node.Id] = node;

            switch (node)
            {
                case ArgumentsNode argsNode when nodeSnap.PortDefinitions.Count != 0:
                {
                    var portDefs = RestorePortDefinitions(nodeSnap.PortDefinitions);
                    argsNode.Initialize(portDefs);
                    break;
                }
                case ReturnNode retNode when nodeSnap.PortDefinitions.Count != 0:
                {
                    var portDefs = RestorePortDefinitions(nodeSnap.PortDefinitions);
                    retNode.Initialize(portDefs);
                    break;
                }
            }

            if (node is ISerializableNode serializable && nodeSnap.CustomData.Any())
                serializable.DeserializeCustomData(nodeSnap.CustomData);

            graph.AddNode(node);
            graph.SetVisualState(node.Id, nodeSnap.X, nodeSnap.Y);
        }

        foreach (var nodeSnap in snapshot.Nodes)
        {
            if (!nodeMap.TryGetValue(nodeSnap.Id, out var node)) continue;

            node.SyncDynamicInputs();

            foreach (var input in nodeSnap.InputsValues)
            {
                if (!node.Inputs.ContainsKey(input.Key)) continue;
                graph.SetInputValue(node.Id, input.Key, input.Value);
            }

            foreach (var subGraphKvp in nodeSnap.SubGraphs)
            {
                var subGraph = Restore(subGraphKvp.Value);
                graph.SetSubgraph(node.Id, subGraphKvp.Key, subGraph);

                AutoBindSubGraphNodes(node, subGraphKvp.Key, subGraph);
            }
        }

        foreach (var conn in snapshot.Connections)
        {
            if (!nodeMap.ContainsKey(conn.FromId) || !nodeMap.ContainsKey(conn.ToId))
                continue;
            graph.Connect(
                conn.FromId, conn.FromPort,
                conn.ToId, conn.ToPort);
        }

        return graph;

        PortDefinition[] RestorePortDefinitions(Dictionary<string, PortDefinitionSnapshot> snapshots)
        {
            return snapshots.Values.Select(snap =>
            {
                var type = Type.GetType(snap.TypeName) ?? typeof(object);
                return new PortDefinition(
                    snap.Name,
                    type,
                    snap.Label,
                    snap.Description,
                    snap.DefaultValue
                );
            }).ToArray();
        }

        void AutoBindSubGraphNodes(NodeLogic node, string subGraphPropName, NodeGraph subGraph)
        {
            var nodeType = node.GetType();
            var subGraphProp = nodeType.GetProperty(subGraphPropName);

            if (subGraphProp == null) return;

            var attr = subGraphProp.GetCustomAttribute<SubGraphAttribute>();
            if (attr == null) return;

            if (!string.IsNullOrEmpty(attr.ArgumentsNodeProperty))
            {
                var argumentsNode = subGraph.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault();
                if (argumentsNode != null)
                {
                    var argsProp = nodeType.GetProperty(attr.ArgumentsNodeProperty);
                    argsProp?.SetValue(node, argumentsNode);
                }
            }

            if (!string.IsNullOrEmpty(attr.ReturnNodeProperty))
            {
                var returnNode = subGraph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault();
                if (returnNode != null)
                {
                    var retProp = nodeType.GetProperty(attr.ReturnNodeProperty);
                    retProp?.SetValue(node, returnNode);
                }
            }
        }
    }
}