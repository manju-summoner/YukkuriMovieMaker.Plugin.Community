using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

[Node(typeof(FunctionCategory), nameof(TextNode.ArgumentsNode), nameof(TextNode.ArgumentsNodeDescription),
    typeof(TextNode))]
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

        InvalidateForce();
    }

    public void InjectArgument(string name, object? value)
    {
        if (Outputs.TryGetValue(name, out var port))
        {
            port.SetValue(value);
            InvalidateForce();
        }
    }

    protected override Task Calculate()
    {
        return Task.CompletedTask;
    }
}