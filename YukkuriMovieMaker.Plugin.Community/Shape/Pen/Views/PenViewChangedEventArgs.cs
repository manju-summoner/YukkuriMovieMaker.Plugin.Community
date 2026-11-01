using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenViewChangedEventArgs(double zoom, Point origin, Size size, double dpiScale) : EventArgs
    {
        public double Zoom { get; } = zoom;

        public Point Origin { get; } = origin;

        public Size Size { get; } = size;

        public double DpiScale { get; } = dpiScale;
    }
}
