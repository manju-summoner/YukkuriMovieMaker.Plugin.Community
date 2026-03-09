using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class TabViewModel : INotifyPropertyChanged
{
    private readonly Action<TabViewModel>? _closeAction;

    public TabViewModel(
        NodeGraph graph,
        string title,
        NodeEditorViewModel nodeEditorViewModel,
        Action<TabViewModel>? closeAction = null)
    {
        Graph = graph;
        Title = title;
        GraphViewModel = new GraphViewModel(graph, nodeEditorViewModel);
        _closeAction = closeAction;

        CloseCommand = new RelayCommand(Close, () => _closeAction != null);
    }

    public string Title { get; }
    public GraphViewModel GraphViewModel { get; }
    public ICommand CloseCommand { get; }
    public NodeGraph Graph { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Close()
    {
        _closeAction?.Invoke(this);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}