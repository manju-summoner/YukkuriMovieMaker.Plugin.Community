using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public abstract class NodeLogic
{
    public readonly Dictionary<string, InputPort> Inputs = new();
    public readonly Dictionary<string, OutputPort> Outputs = new();

    private bool _isEvaluated;

    protected NodeLogic()
    {
        InitializePorts();
    }

    public Guid Id { get; set; }

    public required string Label { get; set; }
    public string Description { get; set; } = "";

    private void InitializePorts()
    {
        var props = GetType().GetProperties();

        foreach (var prop in props)
        {
            if (Attribute.IsDefined(prop, typeof(InputPortAttribute)))
                Inputs.Add(prop.Name, new InputPort(this, prop.PropertyType));

            if (Attribute.IsDefined(prop, typeof(OutputPortAttribute)))
                Outputs.Add(prop.Name, new OutputPort(this, prop.PropertyType));
        }
    }

    public async Task EvaluateInternal()
    {
        if (_isEvaluated) return;

        await Calculate();

        _isEvaluated = true;
    }

    public void Invalidate()
    {
        if (!_isEvaluated) return;

        _isEvaluated = false;

        foreach (var output in Outputs.Values) output.Invalidate();
    }

    protected async Task<T?> GetInputAsync<T>([CallerMemberName] string name = null!)
    {
        var value = await Inputs[name].GetValue();
        return (T?)value;
    }

    protected T? GetInput<T>([CallerMemberName] string name = null!)
    {
        var value = Task.Run(() => Inputs[name].GetValue()).GetAwaiter().GetResult();
        return (T?)value;
    }

    protected void SetInput<T>(T value, [CallerMemberName] string name = null!)
    {
        Inputs[name].SetValue(value);
    }

    protected void SetOutput(object? value, [CallerMemberName] string name = null!)
    {
        Outputs[name].SetValue(value);
    }

    protected async Task<T?> GetOutputAsync<T>([CallerMemberName] string name = null!)
    {
        var value = await Outputs[name].GetValue();
        return (T?)value;
    }

    protected T? GetOutput<T>([CallerMemberName] string name = null!)
    {
        return (T?)Task.Run(() => Outputs[name].GetValue()).GetAwaiter().GetResult();
    }

    protected abstract Task Calculate();
}