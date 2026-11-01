using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    class PenFillGeometry : IPenGeometry
    {
        readonly SerializableStroke stroke;
        readonly ID2D1PathGeometry geometry;

        public int PointCount => 1;

        public int MaxSegmentCount => 0;

        public PenFillGeometry(SerializableStroke stroke, ID2D1Factory factory)
        {
            this.stroke = stroke;
            geometry = Create(stroke, factory);
        }

        static ID2D1PathGeometry Create(SerializableStroke stroke, ID2D1Factory factory)
        {
            var path = factory.CreatePathGeometry();
            using var sink = path.Open();
            sink.SetFillMode(FillMode.Alternate);

            var points = stroke.StylusPoints;
            var figures = stroke.FillFigures ?? [];
            var index = 0;
            for (var i = 0; i < figures.Length; i++)
            {
                var length = figures[i];
                if (length < PenFillFigures.MinLength || index + length > points.Length)
                    break;

                sink.BeginFigure(ToVector(points[index]), FigureBegin.Filled);
                var lines = new Vector2[length - 1];
                for (var j = 1; j < length; j++)
                    lines[j - 1] = ToVector(points[index + j]);
                sink.AddLines(lines);
                sink.EndFigure(FigureEnd.Closed);
                index += length;
            }
            sink.Close();
            return path;
        }

        static Vector2 ToVector(SerializableStylusPoint point) => new((float)point.X, (float)point.Y);

        public void Draw(ID2D1DeviceContext6 dc, int start, int end, double thickness, InkBezierSegment[] segments, PenDrawResources resources)
        {
            var brush = resources.SolidBrushes.GetBrush(dc, stroke.DrawingAttributes.Color.ToColor4());
            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            dc.FillGeometry(geometry, brush);
        }

        public void Dispose()
        {
            geometry.Dispose();
        }
    }
}
