using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenSelectionTransformedEventArgs(Matrix matrix) : EventArgs
    {
        public Matrix Matrix { get; } = matrix;
    }
}
