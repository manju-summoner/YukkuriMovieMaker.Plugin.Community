using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Composition;

public class CompositionCategory : INodeCategory
{
    public string Category => "Effect/Composition";
    public string Color => nameof(Colors.DarkViolet);
}