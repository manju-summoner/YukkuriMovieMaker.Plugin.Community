using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

/// <summary>
///     グラフの出力を受け取るノード。
///     ノードエディタ上では出力端子として機能し、グラフの評価結果をまとめて返す。
/// </summary>
[Node(typeof(FunctionCategory), nameof(TextNode.ReturnNode), nameof(TextNode.ReturnNodeDescription), typeof(TextNode))]
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
            results[name] = await port.GetValue(context).ConfigureAwait(false);
        return results;
    }

    public async Task<object?> ExtractReturn(string name, EvaluationContext? context = null)
    {
        if (Inputs.TryGetValue(name, out var port))
            return await port.GetValue(context).ConfigureAwait(false);
        return null;
    }

    protected override Task Calculate()
    {
        return Task.CompletedTask;
    }
}