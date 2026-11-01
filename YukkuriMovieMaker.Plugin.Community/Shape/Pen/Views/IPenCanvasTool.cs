using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    interface IPenCanvasTool
    {
        bool Begin(Point canvasPoint, float pressure);

        void Move(Point canvasPoint, float pressure);

        void End();

        void Render(DrawingContext drawingContext);

        Cursor GetCursor(Point canvasPoint);
    }
}
