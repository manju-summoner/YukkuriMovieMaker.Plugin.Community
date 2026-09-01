using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Image;

public sealed class ImageSourceCategory : INodeCategory
{
    public string Category => "NodeEffectKey_GeneratorCategoryName/NodeEffectKey_ImageSourceCategoryName";
    public string Color => nameof(Colors.MediumSeaGreen);
}