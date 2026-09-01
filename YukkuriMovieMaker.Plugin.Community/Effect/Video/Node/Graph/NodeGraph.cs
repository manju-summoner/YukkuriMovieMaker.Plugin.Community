using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public sealed class NodeGraph
{
    private static readonly HashSet<Type> NumericPortTypes =
    [
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(short), typeof(ushort), typeof(byte), typeof(sbyte),
        typeof(float), typeof(double), typeof(decimal)
    ];

    private readonly Dictionary<Guid, NodeLogic> _nodes = new();
    public readonly List<NodeConnection> Connections = [];
    public readonly Lock GraphLock = new();
    public readonly Dictionary<string, string> UnresolvedBrushTypeNames = new();

    public readonly List<ConnectionSnapshot> UnresolvedConnections = [];
    public readonly Dictionary<string, string> UnresolvedEffectTypeNames = new();

    public readonly List<NodeSnapshot> UnresolvedNodes = [];
    public readonly Dictionary<Guid, NodeVisualState> VisualStates = new();

    public PreviewNotifier PreviewNotifier { get; } = new();

    public IReadOnlyDictionary<Guid, NodeLogic> Nodes => _nodes;

    /// <summary>
    ///     現在BeginEditとEndEditの間の未確定編集操作の途中かどうか。
    /// </summary>
    public bool IsInTransaction { get; private set; }

    public void UpdateGraph(NodeGraph graph)
    {
        // 旧ノードのD2Dリソースを解放してから差し替える。
        // graph.Nodes は新しいインスタンス群なので Dispose しない。
        foreach (var node in _nodes.Values)
            node.Dispose();

        _nodes.Clear();
        foreach (var node in graph.Nodes) _nodes.Add(node.Key, node.Value);

        Connections.Clear();
        foreach (var connection in graph.Connections) Connections.Add(connection);

        VisualStates.Clear();
        foreach (var graphVisualState in graph.VisualStates)
            VisualStates.Add(graphVisualState.Key, graphVisualState.Value);

        UnresolvedNodes.Clear();
        UnresolvedNodes.AddRange(graph.UnresolvedNodes);

        UnresolvedConnections.Clear();
        UnresolvedConnections.AddRange(graph.UnresolvedConnections);

        UnresolvedEffectTypeNames.Clear();
        foreach (var kv in graph.UnresolvedEffectTypeNames)
            UnresolvedEffectTypeNames[kv.Key] = kv.Value;

        UnresolvedBrushTypeNames.Clear();
        foreach (var kv in graph.UnresolvedBrushTypeNames)
            UnresolvedBrushTypeNames[kv.Key] = kv.Value;
    }

    /// <summary>
    ///     作成済みのノードを管理下に配置します
    /// </summary>
    /// <param name="node">管理対象に追加するノードの演算実体</param>
    public void AddNode(NodeLogic node)
    {
        node.SyncDynamicInputs();

        _nodes[node.Id] = node;

        OnGraphChanged(new NodeAddedEventArgs(node.Id, node));
    }

    /// <summary>
    ///     指定したIDの管理対象のノードを削除します
    /// </summary>
    /// <param name="nodeId">削除するノードのID</param>
    public void RemoveNode(Guid nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        var related = Connections
            .Where(c => c.FromId == nodeId || c.ToId == nodeId)
            .ToList();

        foreach (var c in related)
            Disconnect(c.FromId, c.FromPort, c.ToId, c.ToPort);
        node.Invalidate();
        _nodes.Remove(nodeId);
        // グラフから永久に取り除くのでD2Dリソースを解放する。
        node.Dispose();

        OnGraphChanged(new NodeRemovedEventArgs(nodeId));
    }

    /// <summary>
    ///     指定したIDの管理対象のノードを取得します
    /// </summary>
    /// <param name="nodeId">取得するノードのID</param>
    /// <returns>得られたノードの演算実体</returns>
    public NodeLogic? GetNode(Guid nodeId)
    {
        return _nodes.GetValueOrDefault(nodeId);
    }

    /// <summary>
    ///     指定したIDの管理対象のノードの配置座標データを更新します
    /// </summary>
    /// <param name="nodeId">配置座標データを変更するノードのID</param>
    /// <param name="x">X座標</param>
    /// <param name="y">Y座標</param>
    public void SetVisualState(Guid nodeId, double x, double y)
    {
        var visual = new NodeVisualState { Id = nodeId, X = x, Y = y };
        VisualStates[nodeId] = visual;

        OnGraphChanged(new VisualStateChangedEventArgs(nodeId, visual));

        if (!IsInTransaction)
            Commit();
    }

    /// <summary>
    ///     指定したIDの管理対象のノードの配置座標データを取得します
    /// </summary>
    /// <param name="nodeId">対象のノードのID</param>
    /// <returns>指定したノードの配置座標データ</returns>
    public NodeVisualState? GetVisualState(Guid nodeId)
    {
        VisualStates.TryGetValue(nodeId, out var visualState);
        return visualState;
    }

    /// <summary>
    ///     指定したIDの管理対象のノードの出力プロパティ名を指定して、その値を取得します
    /// </summary>
    /// <param name="nodeId">出力を取得するノードのID</param>
    /// <param name="outputName">値を取得する出力プロパティ名</param>
    /// <returns>値の非同期結果</returns>
    public async Task<object?> GetOutputValue(Guid nodeId, string outputName)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return null;
        return await node.Outputs[outputName].GetValue();
    }

    /// <summary>
    ///     指定したIDの管理対象のノードの入力プロパティ名を指定して、その値を更新します。
    /// </summary>
    /// <param name="nodeId">入力を更新するノードのID</param>
    /// <param name="inputName">値が更新される入力プロパティ名</param>
    /// <param name="value">更新する新しい値</param>
    public void SetInputValue(Guid nodeId, string inputName, object? value)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        if (!node.Inputs.TryGetValue(inputName, out var input)) return;

        input.SetValue(value);
        node.OnInputValueChanged(inputName, value);

        PreviewNotifier.Notify();
        OnGraphChanged(new ValueChangedEventArgs(nodeId, inputName, value));
    }

    /// <summary>
    ///     NodeGraph の外側で、指定ノードの値が直接書き換わったことを通知します
    /// </summary>
    public void NotifyPreviewUpdate(Guid nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
            node.Invalidate();

        PreviewNotifier.Notify();
        OnGraphChanged(new GraphChangedEventArgs());
    }

    /// <summary>
    ///     指定したIDの管理対象のノードの子グラフプロパティ名を指定して、そのグラフを更新します
    /// </summary>
    /// <param name="nodeId">子グラフを更新するノードのID</param>
    /// <param name="subGraphName">グラフが更新される入力プロパティ名</param>
    /// <param name="subGraph">更新する新しいグラフ</param>
    public void SetSubgraph(Guid nodeId, string subGraphName, NodeGraph subGraph)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;

        var prop = node.GetType().GetProperty(subGraphName);
        if (prop != null && prop.PropertyType == typeof(NodeGraph)) prop.SetValue(node, subGraph);

        node.SubGraphs[subGraphName] = subGraph;

        OnGraphChanged(new ValueChangedEventArgs(nodeId, subGraphName, subGraph));
    }

    /// <summary>
    ///     このグラフが管理しているすべてのノードを再計算待ち状態にします
    /// </summary>
    public void InvalidateAll()
    {
        foreach (var node in _nodes.Values) node.Invalidate();
    }

    /// <summary>
    ///     このグラフが管理しているノード２つのそれぞれ指定した入出力プロパティ名同士にデータの依存関係を構築します
    /// </summary>
    /// <param name="from">接続が追加される出力プロパティを持つノードのID</param>
    /// <param name="outputName">接続が追加される出力プロパティ名</param>
    /// <param name="to">接続が設定される入力プロパティを持つノードのID</param>
    /// <param name="inputName">接続が設定される入力プロパティ名</param>
    /// <returns>接続を追加した場合はtrue。循環接続または型が非互換のため拒否した場合はfalse。</returns>
    public bool Connect(
        Guid from, string outputName,
        Guid to, string inputName)
    {
        if (WouldCreateCycle(from, to))
            return false;

        if (!_nodes.TryGetValue(from, out var fromNode) || !fromNode.Outputs.TryGetValue(outputName, out var output))
            return false;
        if (!_nodes.TryGetValue(to, out var toNode) || !toNode.Inputs.TryGetValue(inputName, out var input))
            return false;

        if (!ArePortTypesCompatible(output.ValueType, input.ValueType))
            return false;

        var existing = Connections
            .Where(c => c.ToId == to && c.ToPort == inputName)
            .ToList();

        foreach (var c in existing)
            Disconnect(c.FromId, c.FromPort, c.ToId, c.ToPort);

        input.Connect(output);
        Connections.Add(
            new NodeConnection { FromId = from, FromPort = outputName, ToId = to, ToPort = inputName });

        OnGraphChanged(new ConnectionChangedEventArgs(from, outputName, to, inputName));
        return true;
    }

    /// <summary>
    ///     出力ポートの型を入力ポートへ接続してよいかどうかを判定する。
    /// </summary>
    /// <param name="outputType">接続元の出力ポートの値の型</param>
    /// <param name="inputType">接続先の入力ポートの値の型</param>
    /// <returns>接続を許可できる場合はtrue</returns>
    private static bool ArePortTypesCompatible(Type outputType, Type inputType)
    {
        if (outputType == inputType) return true;
        if (inputType.IsAssignableFrom(outputType)) return true;

        var outputIsNumericLike = NumericPortTypes.Contains(outputType) || outputType.IsEnum;
        var inputIsNumericLike = NumericPortTypes.Contains(inputType) || inputType.IsEnum;
        return outputIsNumericLike && inputIsNumericLike;
    }

    /// <summary>
    ///     指定したノード間に新たに接続（from → to）を追加した場合に、
    ///     グラフが循環（あるノードが自分自身に依存する状態）になるかどうかを判定する。
    ///     既存の接続を to から辿って from に到達できるなら、新しい接続によって循環が生じる。
    /// </summary>
    /// <param name="from">接続の出力側ノードのID</param>
    /// <param name="to">接続の入力側ノードのID</param>
    /// <returns>循環接続になる場合はtrue</returns>
    public bool WouldCreateCycle(Guid from, Guid to)
    {
        if (from == to) return true;

        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(to);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == from) return true;
            if (!visited.Add(current)) continue;

            foreach (var connection in Connections)
                if (connection.FromId == current)
                    stack.Push(connection.ToId);
        }

        return false;
    }

    /// <summary>
    ///     このグラフが管理しているノード２つのそれぞれ指定した入出力プロパティ名同士のデータの依存関係を削除します
    /// </summary>
    /// <param name="from">接続が削除される出力プロパティを持つノードのID</param>
    /// <param name="outputName">接続が削除される出力プロパティ名</param>
    /// <param name="to">接続が削除される入力プロパティを持つノードのID</param>
    /// <param name="inputName">接続が削除される入力プロパティ名</param>
    public void Disconnect(
        Guid from, string outputName,
        Guid to, string inputName)
    {
        if (TryGetPorts(from, outputName, to, inputName, out var output, out var input))
            input.DisConnect(output);

        Connections.RemoveAll(c =>
            c.FromId == from &&
            c.FromPort == outputName &&
            c.ToId == to &&
            c.ToPort == inputName);

        OnGraphChanged(new ConnectionChangedEventArgs(from, outputName, null, null));
        OnGraphChanged(new ConnectionChangedEventArgs(null, null, to, inputName));
    }

    internal void DisconnectSilently(
        Guid from, string outputName,
        Guid to, string inputName)
    {
        if (TryGetPorts(from, outputName, to, inputName, out var output, out var input))
            input.DisConnect(output);

        Connections.RemoveAll(c =>
            c.FromId == from &&
            c.FromPort == outputName &&
            c.ToId == to &&
            c.ToPort == inputName);
    }

    private bool TryGetPorts(
        Guid from, string outputName, Guid to, string inputName,
        out OutputPort output, out InputPort input)
    {
        output = null!;
        input = null!;

        if (!_nodes.TryGetValue(from, out var fromNode) || !fromNode.Outputs.TryGetValue(outputName, out var o))
            return false;
        if (!_nodes.TryGetValue(to, out var toNode) || !toNode.Inputs.TryGetValue(inputName, out var i))
            return false;

        output = o;
        input = i;
        return true;
    }

    /// <summary>
    ///     このグラフが変更され、ノードの入出力の値が更新された可能性があることを通知します
    /// </summary>
    public event EventHandler<GraphChangedEventArgs>? GraphChanged;

    /// <summary>
    ///     このグラフに対し行われた変更操作が内部から確定されたことを通知します
    /// </summary>
    public event EventHandler<CommittedEventArgs>? Committed;

    public void OnGraphChanged(GraphChangedEventArgs args)
    {
        args.IsInTransaction = IsInTransaction;
        GraphChanged?.Invoke(this, args);
    }

    public void Commit()
    {
        Committed?.Invoke(this, new CommittedEventArgs());
    }

    /// <summary>
    ///     このグラフに対して何らかの編集操作が開始することを通達します
    /// </summary>
    public void BeginEdit()
    {
        IsInTransaction = true;
    }

    /// <summary>
    ///     このグラフに対して行われた編集操作を完了し、確定します。
    ///     値の「確定」に相当し、PreviewNotifier 経由のプレビュー更新に加えて、
    ///     Commit（Undo履歴の記録）と全ノードの再計算を行う。
    /// </summary>
    public void EndEdit()
    {
        if (!IsInTransaction) return;
        IsInTransaction = false;

        PreviewNotifier.Notify();
        OnGraphChanged(new GraphChangedEventArgs());

        Commit();
        InvalidateAll();
    }
}