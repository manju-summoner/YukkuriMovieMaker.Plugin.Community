using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

public class ReturnNode : NodeLogic
{
    public ReturnNode(params PortDefinition[] returnPorts)
    {
        InitializeReturnPorts(returnPorts);
    }

    private void InitializeReturnPorts(PortDefinition[] returnPorts)
    {
        foreach (var portDef in returnPorts)
        {
            var port = new InputPort(this, portDef.ValueType);
            Inputs.Add(portDef.Name, port);

            if (portDef.DefaultValue != null) port.SetValue(portDef.DefaultValue);
        }
    }

    public async Task<Dictionary<string, object?>> ExtractReturns()
    {
        var results = new Dictionary<string, object?>();
        foreach (var (name, port) in Inputs)
        {
            var value = await port.GetValue();
            results[name] = value;
        }

        return results;
    }

    protected override Task Calculate()
    {
        return Task.CompletedTask;
    }
}