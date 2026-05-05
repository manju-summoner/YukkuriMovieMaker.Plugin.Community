using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Brush;

public sealed class BrushCategory : INodeCategory
{
    public string Category => "Effect/Generator/Brush";
    public string Color => nameof(Colors.LawnGreen);
}