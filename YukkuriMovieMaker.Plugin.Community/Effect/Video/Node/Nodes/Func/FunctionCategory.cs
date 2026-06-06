using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;

public sealed class FunctionCategory : INodeCategory
{
    public string Category => "NodeEffectKey_FunctionsCategoryName";
    public string Color => nameof(Colors.SlateGray);
}