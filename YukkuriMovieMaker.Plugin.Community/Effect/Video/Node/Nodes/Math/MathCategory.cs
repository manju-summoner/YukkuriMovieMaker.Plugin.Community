using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Math;

public sealed class MathBasicCategory : INodeCategory
{
    public string Category => "NodeEffectKey_MathCategoryName/NodeEffectKey_BasicCategoryName";
    public string Color => nameof(Colors.DarkOrange);
}

public sealed class MathFunctionsCategory : INodeCategory
{
    public string Category => "NodeEffectKey_MathCategoryName/NodeEffectKey_FunctionsCategoryName";
    public string Color => nameof(Colors.DarkOrange);
}

public sealed class MathAdvancedCategory : INodeCategory
{
    public string Category => "NodeEffectKey_MathCategoryName/NodeEffectKey_AdvancedCategoryName";
    public string Color => nameof(Colors.DarkOrange);
}