using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    class PenFillRequestedEventArgs(Point point) : EventArgs
    {
        public Point Point { get; } = point;
    }
}
