using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

public class ReturnNode : NodeLogic
{
    private PortDefinition[] _portDefinitions = Array.Empty<PortDefinition>();

    public ReturnNode()
    {
    }

    public ReturnNode(params PortDefinition[] returnPorts)
    {
        Initialize(returnPorts);
    }

    public void Initialize(PortDefinition[] returnPorts)
    {
        _portDefinitions = returnPorts;
        Inputs.Clear();

        foreach (var portDef in returnPorts)
        {
            var port = new InputPort(this, portDef.ValueType);
            Inputs.Add(portDef.Name, port);

            if (portDef.DefaultValue != null) port.SetValue(portDef.DefaultValue);
        }
    }

    public PortDefinition[] GetPortDefinitions()
    {
        return _portDefinitions;
    }

    public async Task<Dictionary<string, object?>> ExtractReturns(EvaluationContext? context = null)
    {
        var results = new Dictionary<string, object?>();
        foreach (var (name, port) in Inputs)
        {
            var value = await port.GetValue(context);
            results[name] = value;
        }

        return results;
    }

    public async Task<object?> ExtractReturn(string name, EvaluationContext? context = null)
    {
        if (Inputs.TryGetValue(name, out var port)) return await port.GetValue(context);

        return null;
    }

    protected override Task Calculate()
    {
        return Task.CompletedTask;
    }
}