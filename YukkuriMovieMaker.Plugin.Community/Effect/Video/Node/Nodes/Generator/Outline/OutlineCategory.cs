using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Outline;

public sealed class OutlineCategory : INodeCategory
{
    public string Category => "NodeEffectKey_GeneratorCategoryName/アウトライン";
    public string Color => nameof(Colors.Goldenrod);
}