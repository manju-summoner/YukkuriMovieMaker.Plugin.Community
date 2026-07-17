using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.Transform;

public class TransformCategory : INodeCategory
{
    public string Category => "NodeEffectKey_EffectCategoryName/NodeEffectKey_TransformCategoryName";
    public string Color => nameof(Colors.DarkCyan);
}