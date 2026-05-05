namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

/// <summary>
///     ノードへの入力ポート。
///     上流の OutputPort に接続されている場合は上流ノードを評価して値を取得する。
///     未接続の場合は SetValue で直接書き込まれた値を返す。
/// </summary>
public sealed class InputPort : Port
{
    private OutputPort? _outputPort;
    private object? _value;

    public InputPort(NodeLogic owner, Type valueType) : base(owner, valueType)
    {
    }

    public bool IsConnected => _outputPort is not null;

    public object? LocalValue => _outputPort is null ? _value : null;

    public void Connect(OutputPort outputPort)
    {
        outputPort.RegisterConnection(this);
        _outputPort = outputPort;
        Owner.Invalidate();
    }

    public void DisConnect(OutputPort outputPort)
    {
        outputPort.UnregisterConnection(this);
        _outputPort = null;
        Owner.Invalidate();
    }

    public void SetValue(object? value)
    {
        _value = value;
        Owner.Invalidate();
    }

    public async Task<object?> GetValue()
    {
        if (_outputPort is null) return _value;
        return await _outputPort.GetValue().ConfigureAwait(false);
    }

    public async Task<object?> GetValue(EvaluationContext? context)
    {
        if (_outputPort is null) return _value;
        return await _outputPort.GetValue(context).ConfigureAwait(false);
    }
}