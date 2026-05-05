using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

/// <summary>
///     サブグラフを引数付きで実行し、戻り値を取得するヘルパー。
///     ArgumentsNode に引数を注入してグラフ全体を無効化した後、
///     ReturnNode から結果を取り出す。
/// </summary>
public class Executor(
    NodeGraph subGraph,
    ArgumentsNode argumentsNode,
    ReturnNode returnNode)
{
    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> arguments)
    {
        argumentsNode.InjectArguments(arguments);
        subGraph.InvalidateAll();
        return await returnNode.ExtractReturns().ConfigureAwait(false);
    }
}