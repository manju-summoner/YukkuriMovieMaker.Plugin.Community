using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

public class ArgumentsNode : NodeLogic
{
    public ArgumentsNode(params PortDefinition[] argumentPorts)
    {
        InitializeArgumentPorts(argumentPorts);
    }

    private void InitializeArgumentPorts(PortDefinition[] argumentPorts)
    {
        foreach (var portDef in argumentPorts)
        {
            var port = new OutputPort(this, portDef.ValueType);
            Outputs.Add(portDef.Name, port);

            if (portDef.DefaultValue != null) port.SetValue(portDef.DefaultValue);
        }
    }

    public void InjectArguments(Dictionary<string, object?> arguments)
    {
        foreach (var (name, value) in arguments)
            if (Outputs.TryGetValue(name, out var port))
                port.SetValue(value);

        Invalidate();
    }

    protected override Task Calculate()
    {
        return Task.CompletedTask;
    }
}