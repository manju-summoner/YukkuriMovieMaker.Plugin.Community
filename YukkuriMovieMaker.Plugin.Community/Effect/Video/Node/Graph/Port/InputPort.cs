namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

public sealed class InputPort : Port
{
    private OutputPort? _outputPort;
    private object? _value;

    public InputPort(NodeLogic owner, Type valueType) : base(owner, valueType)
    {
    }

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

        return await _outputPort.GetValue();
    }

    public async Task<object?> GetValue(EvaluationContext? context)
    {
        if (_outputPort is null) return _value;
        return await _outputPort.GetValue(context);
    }
}