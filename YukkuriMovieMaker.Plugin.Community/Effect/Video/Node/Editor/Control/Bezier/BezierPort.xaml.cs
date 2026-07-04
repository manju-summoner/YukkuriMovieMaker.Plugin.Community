using System.Windows;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier;

public partial class BezierPort
{
    public BezierPort()
    {
        InitializeComponent();
    }

    internal void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        BeginEditCommand?.Execute(null);
        EndEditCommand?.Execute(null);
    }

    internal void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            BeginEditCommand?.Execute(null);
            EndEditCommand?.Execute(null);
        }
    }
}