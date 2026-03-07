using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class SubGraphViewModel
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
        _graph.GraphChanged += (_, args) => OnGraphChangedCommand?.Execute(args);
        _graph.Committed += (_, _) => OnGraphCommitedCommand?.Execute(null);
    }

    public string Name { get; }
    public string Label { get; }
    public string Description { get; }

    public ICommand? OpenSubGraphCommand { get; init; }
    public ICommand? OnGraphChangedCommand { get; init; }
    public ICommand? OnGraphCommitedCommand { get; init; }
}