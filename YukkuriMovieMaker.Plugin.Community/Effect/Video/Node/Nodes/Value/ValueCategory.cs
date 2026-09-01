using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Value;

public sealed class ValueCategory : INodeCategory
{
    public string Category => "NodeEffectKey_ValueCategoryName";
    public string Color => nameof(Colors.DarkOrange);
}

public sealed class StringCategory : INodeCategory
{
    public string Category => "NodeEffectKey_ValueCategoryName/NodeEffectKey_StringCategoryName";
    public string Color => nameof(Colors.DarkOrange);
}

public sealed class ColorCategory : INodeCategory
{
    public string Category => "NodeEffectKey_ValueCategoryName/NodeEffectKey_ColorCategoryName";
    public string Color => nameof(Colors.Gold);
}