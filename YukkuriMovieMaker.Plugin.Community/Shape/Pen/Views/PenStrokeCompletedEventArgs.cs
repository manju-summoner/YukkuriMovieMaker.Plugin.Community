using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenStrokeCompletedEventArgs(StylusPointCollection stylusPoints, bool isEraser) : EventArgs
    {
        public StylusPointCollection StylusPoints { get; } = stylusPoints;

        public bool IsEraser { get; } = isEraser;
    }
}
