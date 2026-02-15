using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

public class ArgumentsNode : NodeLogic
{
    private PortDefinition[] _portDefinitions = Array.Empty<PortDefinition>();

    public ArgumentsNode()
    {
    }

    public ArgumentsNode(params PortDefinition[] argumentPorts)
    {
        Initialize(argumentPorts);
    }

    public void Initialize(PortDefinition[] argumentPorts)
    {
        _portDefinitions = argumentPorts;
        Outputs.Clear();

        foreach (var portDef in argumentPorts)
        {
            var port = new OutputPort(this, portDef.ValueType);
            Outputs.Add(portDef.Name, port);

            if (portDef.DefaultValue != null) port.SetValue(portDef.DefaultValue);
        }
    }

    public PortDefinition[] GetPortDefinitions()
    {
        return _portDefinitions;
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