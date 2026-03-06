using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor;

public class NodeEditor : IToolPlugin
{
    public string Name => TextUi.Node;
    public Type ViewModelType => typeof(NodeEditorViewModel);
    public Type ViewType => typeof(NodeEditorView);
    public bool AllowMultipleInstances => true;
}