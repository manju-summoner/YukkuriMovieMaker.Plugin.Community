using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor;

public class NodeEditor : IToolPlugin
{
    public string Name => TextUi.Node;
    public Type ViewModelType { get; }
    public Type ViewType { get; }
}