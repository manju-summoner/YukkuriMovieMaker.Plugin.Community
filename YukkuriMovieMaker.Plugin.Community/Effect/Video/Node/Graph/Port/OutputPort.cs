namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

public class OutputPort : Port
{
    private readonly HashSet<InputPort> _connection = [];
    private object? _cachedValue;
    private bool _isCached;

    public OutputPort(NodeLogic owner, Type valueType) : base(owner, valueType)
    {
    }

    public void SetValue(object? value)
    {
        _cachedValue = value;
        _isCached = true;
    }

    public async Task<object?> GetValue()
    {
        if (!_isCached) await Owner.EvaluateInternal().ConfigureAwait(false);
        return _cachedValue;
    }

    public async Task<object?> GetValue(EvaluationContext? context)
    {
        if (!_isCached) await Owner.EvaluateInternal(context).ConfigureAwait(false);
        return _cachedValue;
    }

    internal void RegisterConnection(InputPort inputPort)
    {
        _connection.Add(inputPort);
    }

    internal void UnregisterConnection(InputPort inputPort)
    {
        _connection.Remove(inputPort);
    }

    /// <summary>
    ///     キャッシュを無効化し、下流ノードにも無効化を伝播する。
    ///     すでに無効化済みであれば伝播を止める。
    /// </summary>
    internal void Invalidate()
    {
        if (!_isCached) return;
        _isCached = false;
        foreach (var input in _connection) input.Owner.Invalidate();
    }
}