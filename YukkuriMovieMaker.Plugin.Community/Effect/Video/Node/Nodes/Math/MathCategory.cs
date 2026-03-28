using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Math;

public sealed class MathBasicCategory : INodeCategory
{
    public string Category => "Math/Basic";
    public string Color => nameof(Colors.DarkOrange);
}