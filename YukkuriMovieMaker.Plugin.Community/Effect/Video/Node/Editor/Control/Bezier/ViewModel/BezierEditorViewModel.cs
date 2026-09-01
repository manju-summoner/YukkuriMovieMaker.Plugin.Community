using System.ComponentModel;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.ViewModel;

public sealed class BezierEditorViewModel : INotifyPropertyChanged
{
    public BezierEditorViewModel(BezierCurve curve)
    {
        Curve = curve;
    }

    public BezierCurve Curve { get; }

    public BezierNode? SelectedNode
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}