using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenFillFigures
    {
        public const int MinLength = 3;

        public static readonly Guid PropertyId = new("7a5cf2a3-b314-4a03-ac1a-05cffec30ef0");

        public static bool IsValid(int[] figures, int pointCount)
        {
            var remaining = pointCount;
            foreach (var length in figures)
            {
                if (length < MinLength || length > remaining)
                    return false;
                remaining -= length;
            }
            return remaining == 0;
        }

        public static Geometry CreateGeometry(SerializableStroke stroke)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
            using (var context = geometry.Open())
            {
                var points = stroke.StylusPoints;
                var figures = stroke.FillFigures ?? [];
                var index = 0;
                for (var i = 0; i < figures.Length; i++)
                {
                    var length = figures[i];
                    if (length < MinLength || index + length > points.Length)
                        break;

                    context.BeginFigure(new Point(points[index].X, points[index].Y), true, true);
                    for (var j = 1; j < length; j++)
                        context.LineTo(new Point(points[index + j].X, points[index + j].Y), false, false);
                    index += length;
                }
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
