using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    sealed class PenFillTool(PenEditorCanvas canvas) : IPenCanvasTool
    {
        public bool Begin(Point canvasPoint, float pressure)
        {
            canvas.RaiseFillRequested(new PenFillRequestedEventArgs(canvasPoint));
            return false;
        }

        public void Move(Point canvasPoint, float pressure)
        {
        }

        public void End()
        {
        }

        public void Render(DrawingContext drawingContext)
        {
        }

        public Cursor GetCursor(Point canvasPoint) => Cursors.Cross;
    }
}
