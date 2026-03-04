using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public class SubGraphViewModel
{
    private readonly NodeGraph _graph;

    public SubGraphViewModel(
        string name,
        string label,
        string description,
        NodeGraph graph)
    {
        Name = name;
        Label = label;
        Description = description;
        _graph = graph;
    }

    public string Name { get; }
    public string Label { get; }
    public string Description { get; }

    public ICommand? OpenSubGraphCommand { get; }
}