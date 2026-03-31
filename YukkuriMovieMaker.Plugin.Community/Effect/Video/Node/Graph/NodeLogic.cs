using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public abstract class NodeLogic
{
    public readonly Dictionary<string, InputPort> Inputs = new();
    public readonly Dictionary<string, OutputPort> Outputs = new();
    public readonly Dictionary<string, NodeGraph> SubGraphs = new();

    private bool _isEvaluated;

    protected NodeLogic()
    {
        InitializePorts();
    }

    public Guid Id { get; set; }

    protected EvaluationContext? EvaluationContext { get; private set; }

    private void InitializePorts()
    {
        var props = GetType().GetProperties();

        foreach (var prop in props)
        {
            if (Attribute.IsDefined(prop, typeof(InputPortAttribute)))
                Inputs.Add(prop.Name, new InputPort(this, prop.PropertyType));

            if (Attribute.IsDefined(prop, typeof(OutputPortAttribute)))
                Outputs.Add(prop.Name, new OutputPort(this, prop.PropertyType));

            if (Attribute.IsDefined(prop, typeof(SubGraphAttribute)) && prop.GetValue(this) is NodeGraph subGraph)
                SubGraphs.Add(prop.Name, subGraph);
        }
    }

    public void UpdateSubGraphs()
    {
        var props = GetType().GetProperties();

        foreach (var prop in props)
            if (Attribute.IsDefined(prop, typeof(SubGraphAttribute)) &&
                prop.PropertyType == typeof(NodeGraph))
            {
                if (prop.GetValue(this) is NodeGraph subGraph)
                    SubGraphs[prop.Name] = subGraph;
                else
                    SubGraphs.Remove(prop.Name);
            }
    }

    public async Task EvaluateInternal(EvaluationContext? context = null)
    {
        if (_isEvaluated) return;
        EvaluationContext = context;
        var success = false;
        try
        {
            await Calculate();
            success = true;
        }
        catch (NullReferenceException)
        {
            if (context != null) throw;
        }
        finally
        {
            EvaluationContext = null;
            if (success) _isEvaluated = true;
        }
    }

    public void Invalidate()
    {
        if (!_isEvaluated) return;

        _isEvaluated = false;

        foreach (var output in Outputs.Values) output.Invalidate();
    }

    protected async Task<T?> GetInputAsync<T>([CallerMemberName] string name = null!)
    {
        var value = await Inputs[name].GetValue(EvaluationContext);
        if (value is null) return default;
        return (T?)Convert.ChangeType(value, typeof(T));
    }

    protected T? GetInput<T>([CallerMemberName] string name = null!)
    {
        var context = EvaluationContext;
        var value = Task.Run(() => Inputs[name].GetValue(context)).GetAwaiter().GetResult();
        if (value is null) return default;
        return (T?)Convert.ChangeType(value, typeof(T));
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
        if (value is null) return default;
        return (T?)Convert.ChangeType(value, typeof(T));
    }

    protected T? GetOutput<T>([CallerMemberName] string name = null!)
    {
        var value = Task.Run(() => Outputs[name].GetValue()).GetAwaiter().GetResult();
        if (value is null) return default;
        return (T?)Convert.ChangeType(value, typeof(T));
    }

    protected abstract Task Calculate();
}