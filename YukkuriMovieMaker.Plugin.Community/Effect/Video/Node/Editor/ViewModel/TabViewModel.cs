using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

public sealed class TabViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Action<TabViewModel>? _closeAction;

    public TabViewModel(
        NodeGraph graph,
        string title,
        NodeEditorViewModel nodeEditorViewModel,
        Action<TabViewModel>? closeAction = null,
        IEditorInfo? editorInfo = null)
    {
        Graph = graph;
        Title = title;
        GraphViewModel = new GraphViewModel(graph, nodeEditorViewModel, editorInfo);
        _closeAction = closeAction;

        CloseCommand = new RelayCommand(Close, () => _closeAction != null);
    }

    public string Title { get; }
    public GraphViewModel GraphViewModel { get; }
    public ICommand CloseCommand { get; }
    public NodeGraph Graph { get; }

    public void Dispose()
    {
        GraphViewModel.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     開いた後に IEditorInfo が更新された場合（OpenNodeEditorButton.SetEditorInfo の再呼び出し等）に、
    ///     このタブのグラフへ最新の値を反映する。
    /// </summary>
    internal void SetEditorInfo(IEditorInfo? info)
    {
        GraphViewModel.EditorInfo = info;
    }

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