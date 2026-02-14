using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public class Executor(
    NodeGraph subGraph,
    ArgumentsNode argumentsNode,
    ReturnNode returnNode)
{
    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> arguments)
    {
        argumentsNode.InjectArguments(arguments);
        subGraph.InvalidateAll();
        var results = await returnNode.ExtractReturns();

        return results;
    }
}