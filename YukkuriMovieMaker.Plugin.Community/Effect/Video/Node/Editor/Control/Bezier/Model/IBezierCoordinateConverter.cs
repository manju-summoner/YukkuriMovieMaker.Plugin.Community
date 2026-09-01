using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;

public interface IBezierCoordinateConverter
{
    Point ToScreen(Point point);

    Point FromScreen(Point point);
}