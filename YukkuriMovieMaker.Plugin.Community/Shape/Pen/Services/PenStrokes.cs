using System.Collections.Immutable;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services
{
    static class PenStrokes
    {
        static readonly Guid PencilPropertyId = new("3c9d5b2e-6f41-4a8c-9d07-2e5b8c1f6a3d");

        const int LassoPercentage = 80;
        const double MinStylusSize = 3.77952755905512E-05;
        const double MaxStylusSize = 162329.461417323;

        public static ImmutableList<int> HitTest(ImmutableList<SerializableStroke> source, IReadOnlyList<Point> lassoPoints)
        {
            var strokes = new StrokeCollection();
            foreach (var serializable in source)
                strokes.Add(serializable.ToStroke());

            var hits = strokes.HitTest(lassoPoints, LassoPercentage);
            if (hits.Count == 0)
                return [];

            var hitStrokes = new HashSet<Stroke>(hits);
            var indices = ImmutableList.CreateBuilder<int>();
            for (var i = 0; i < strokes.Count; i++)
            {
                if (hitStrokes.Contains(strokes[i]))
                    indices.Add(i);
            }
            return indices.ToImmutable();
        }

        public static ImmutableList<SerializableStroke> Transform(ImmutableList<SerializableStroke> source, ImmutableList<int> indices, Matrix matrix)
        {
            var scale = Math.Sqrt(Math.Abs(matrix.Determinant));
            var builder = source.ToBuilder();
            foreach (var index in indices)
            {
                if (index >= source.Count)
                    continue;

                var stroke = source[index];
                var points = new SerializableStylusPoint[stroke.StylusPoints.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    var point = stroke.StylusPoints[i];
                    var moved = matrix.Transform(new Point(point.X, point.Y));
                    points[i] = new SerializableStylusPoint(moved.X, moved.Y, point.PressureFactor);
                }
                builder[index] = stroke with { StylusPoints = points, DrawingAttributes = ScaleAttributes(stroke.DrawingAttributes, scale) };
            }
            return builder.ToImmutable();
        }

        static DrawingAttributes ScaleAttributes(DrawingAttributes attributes, double scale)
        {
            if (scale == 1)
                return attributes;

            var scaled = attributes.Clone();
            scaled.Width = ClampStylusSize(attributes.Width * scale);
            scaled.Height = ClampStylusSize(attributes.Height * scale);
            return scaled;
        }

        static double ClampStylusSize(double size)
        {
            if (double.IsNaN(size) || size < MinStylusSize)
                return MinStylusSize;
            return size > MaxStylusSize ? MaxStylusSize : size;
        }

        public static Rect GetBounds(ImmutableList<SerializableStroke> strokes, ImmutableList<int> indices)
        {
            var bounds = Rect.Empty;
            foreach (var index in indices)
            {
                if (index < strokes.Count)
                    bounds.Union(GetBounds(strokes[index]));
            }
            return bounds;
        }

        public static Rect GetBounds(SerializableStroke stroke)
        {
            var points = stroke.StylusPoints;
            if (points.Length == 0)
                return Rect.Empty;

            GetTipExtents(stroke, out var width, out var height);
            var ignoresPressure = stroke.DrawingAttributes.IgnorePressure;
            var left = double.MaxValue;
            var top = double.MaxValue;
            var right = double.MinValue;
            var bottom = double.MinValue;
            foreach (var point in points)
            {
                var pressure = ignoresPressure ? SerializableStylusPoint.NeutralPressure : point.PressureFactor;
                var radiusX = width * pressure;
                var radiusY = height * pressure;
                left = Math.Min(left, point.X - radiusX);
                top = Math.Min(top, point.Y - radiusY);
                right = Math.Max(right, point.X + radiusX);
                bottom = Math.Max(bottom, point.Y + radiusY);
            }
            return new Rect(left, top, right - left, bottom - top);
        }

        static void GetTipExtents(SerializableStroke stroke, out double extentX, out double extentY)
        {
            if (stroke.FillFigures is not null)
            {
                extentX = 0;
                extentY = 0;
                return;
            }

            var attributes = stroke.DrawingAttributes;
            var tip = attributes.StylusTipTransform;
            if (tip.IsIdentity)
            {
                extentX = attributes.Width;
                extentY = attributes.Height;
                return;
            }

            var x11 = attributes.Width * tip.M11;
            var x21 = attributes.Height * tip.M21;
            var y12 = attributes.Width * tip.M12;
            var y22 = attributes.Height * tip.M22;
            if (attributes.StylusTip is StylusTip.Ellipse)
            {
                extentX = Math.Sqrt(x11 * x11 + x21 * x21);
                extentY = Math.Sqrt(y12 * y12 + y22 * y22);
                return;
            }

            extentX = Math.Abs(x11) + Math.Abs(x21);
            extentY = Math.Abs(y12) + Math.Abs(y22);
        }

        public static Matrix CreateFlip(Rect bounds, bool isHorizontal)
        {
            var matrix = Matrix.Identity;
            matrix.ScaleAt(
                isHorizontal ? -1 : 1,
                isHorizontal ? 1 : -1,
                bounds.X + bounds.Width / 2,
                bounds.Y + bounds.Height / 2);
            return matrix;
        }

        public static ImmutableList<SerializableStroke> Remove(ImmutableList<SerializableStroke> strokes, ImmutableList<int> indices)
        {
            var builder = strokes.ToBuilder();
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                var index = indices[i];
                if (index < builder.Count)
                    builder.RemoveAt(index);
            }
            return builder.ToImmutable();
        }

        public static ImmutableList<SerializableStroke> Duplicate(ImmutableList<SerializableStroke> source, ImmutableList<int> indices, out ImmutableList<int> duplicated)
        {
            var builder = source.ToBuilder();
            var created = ImmutableList.CreateBuilder<int>();
            foreach (var index in indices)
            {
                if (index >= source.Count)
                    continue;

                created.Add(builder.Count);
                builder.Add(source[index]);
            }
            duplicated = created.ToImmutable();
            return builder.ToImmutable();
        }

        public static ImmutableList<SerializableStroke> Extract(ImmutableList<SerializableStroke> source, ImmutableList<int> indices, out ImmutableList<SerializableStroke> extracted)
        {
            var moved = ImmutableList.CreateBuilder<SerializableStroke>();
            var remaining = source.ToBuilder();
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                var index = indices[i];
                if (index >= remaining.Count)
                    continue;

                moved.Insert(0, remaining[index]);
                remaining.RemoveAt(index);
            }
            extracted = moved.ToImmutable();
            return remaining.ToImmutable();
        }

        public static ImmutableList<SerializableStroke> Append(ImmutableList<SerializableStroke> source, IReadOnlyList<SerializableStroke> items, out ImmutableList<int> appended)
        {
            var builder = source.ToBuilder();
            var indices = ImmutableList.CreateBuilder<int>();
            foreach (var item in items)
            {
                indices.Add(builder.Count);
                builder.Add(item);
            }
            appended = indices.ToImmutable();
            return builder.ToImmutable();
        }

        public static ImmutableList<SerializableStroke>? Apply(ImmutableList<SerializableStroke> strokes, ImmutableList<int> indices, Func<SerializableStroke, DrawingAttributes?> convert)
        {
            var builder = strokes.ToBuilder();
            var isChanged = false;
            foreach (var index in indices)
            {
                if (index >= builder.Count)
                    continue;

                var stroke = builder[index];
                var attributes = convert(stroke);
                if (attributes is null)
                    continue;

                builder[index] = stroke with { DrawingAttributes = attributes };
                isChanged = true;
            }
            return isChanged ? builder.ToImmutable() : null;
        }

        public static DrawingAttributes? WithColor(SerializableStroke stroke, Color color)
        {
            var attributes = stroke.DrawingAttributes;
            if (attributes.Color == color)
                return null;

            var changed = attributes.Clone();
            changed.Color = color;
            return changed;
        }

        public static DrawingAttributes? WithThickness(SerializableStroke stroke, double thickness)
        {
            if (stroke.FillFigures is not null)
                return null;

            var attributes = stroke.DrawingAttributes;
            var height = attributes.Height;
            if (height <= 0)
                return null;

            var width = ClampStylusSize(thickness * attributes.Width / height);
            var scaled = ClampStylusSize(thickness);
            if (attributes.Height == scaled && attributes.Width == width)
                return null;

            var changed = attributes.Clone();
            changed.Width = width;
            changed.Height = scaled;
            return changed;
        }

        public static ImmutableList<SerializableStroke>? Erase(ImmutableList<SerializableStroke> strokes, StylusPointCollection stylusPoints, double size, bool isLine)
        {
            var shape = new EllipseStylusShape(size, size);
            var path = new List<Point>(stylusPoints.Count);
            foreach (var point in stylusPoints)
                path.Add(new Point(point.X, point.Y));

            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            var isErased = false;
            foreach (var serializable in strokes)
            {
                var isFill = serializable.FillFigures is not null;
                var stroke = serializable.ToStroke();
                if (!(isFill ? IsFillErased(serializable, stroke, path, shape) : stroke.HitTest(path, shape)))
                {
                    builder.Add(serializable);
                    continue;
                }

                isErased = true;
                if (isLine || isFill)
                    continue;

                foreach (var erased in stroke.GetEraseResult(path, shape))
                    builder.Add(serializable with { StylusPoints = SerializableStroke.ToPoints(erased.StylusPoints), DrawingAttributes = erased.DrawingAttributes });
            }

            return isErased ? builder.ToImmutable() : null;
        }

        static bool IsFillErased(SerializableStroke serializable, Stroke stroke, List<Point> path, StylusShape shape)
        {
            if (stroke.HitTest(path, shape))
                return true;

            var geometry = PenFillFigures.CreateGeometry(serializable);
            foreach (var point in path)
            {
                if (geometry.FillContains(point))
                    return true;
            }
            return false;
        }

        public static bool IsWellFormed(SerializableStroke? stroke)
        {
            if (stroke is not { DrawingAttributes: not null, StylusPoints: { Length: > 0 } points })
                return false;

            foreach (var point in points)
            {
                if (point is null || !double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.PressureFactor is < 0 or > 1 or float.NaN)
                    return false;
            }
            return stroke.FillFigures is null || PenFillFigures.IsValid(stroke.FillFigures, points.Length);
        }

        public static Stroke ToIsfStroke(SerializableStroke serializable)
        {
            var stroke = serializable.ToStroke();
            if (serializable.FillFigures is { } figures)
                stroke.AddPropertyData(PenFillFigures.PropertyId, figures);
            if (serializable.IsPencil)
                stroke.AddPropertyData(PencilPropertyId, true);
            return stroke;
        }

        public static SerializableStroke FromIsfStroke(Stroke stroke)
        {
            var figures = stroke.ContainsPropertyData(PenFillFigures.PropertyId)
                ? stroke.GetPropertyData(PenFillFigures.PropertyId) as int[]
                : null;
            var isPencil = stroke.ContainsPropertyData(PencilPropertyId) && stroke.GetPropertyData(PencilPropertyId) is true;
            return new SerializableStroke(stroke) { FillFigures = figures, IsPencil = isPencil };
        }
    }
}
