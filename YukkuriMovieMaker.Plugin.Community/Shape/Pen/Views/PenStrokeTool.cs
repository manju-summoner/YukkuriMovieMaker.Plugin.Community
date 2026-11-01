using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    sealed class PenStrokeTool : IPenCanvasTool
    {
        const double StabilizationSettleDistance = 0.5;

        readonly PenEditorCanvas canvas;

        readonly DrawingVisual wetInkVisual = new();
        readonly DrawingGroup wetInkDrawing = new();
        readonly MatrixTransform wetInkTransform = new();

        System.Windows.Media.Brush wetInkBrush = Brushes.White;
        System.Windows.Media.Pen? wetInkPen;
        double wetInkPenThickness;
        StylusPointCollection? strokePoints;
        double[] taperDistances = [];
        Point rawPoint;
        bool isOverlay;

        public PenStrokeTool(PenEditorCanvas canvas)
        {
            this.canvas = canvas;
            wetInkVisual.Transform = wetInkTransform;
            using (var context = wetInkVisual.RenderOpen())
                context.DrawDrawing(wetInkDrawing);
        }

        public Visual Visual => wetInkVisual;

        public StylusPointCollection? WetPoints => isOverlay ? null : strokePoints;

        public bool Begin(Point canvasPoint, float pressure)
        {
            wetInkDrawing.Children.Clear();
            isOverlay = canvas.IsWetInkOverlay;
            strokePoints = [new StylusPoint(canvasPoint.X, canvasPoint.Y, GetPressure(pressure))];
            rawPoint = canvasPoint;
            if (!isOverlay)
                canvas.RaiseWetStrokeChanged();
            return true;
        }

        public void Move(Point canvasPoint, float pressure)
        {
            if (strokePoints is null)
                return;

            rawPoint = canvasPoint;
            var previous = strokePoints[^1];
            var point = Stabilize(previous, canvasPoint, GetPressure(pressure));
            if (previous.X == point.X && previous.Y == point.Y)
                return;

            strokePoints.Add(point);
            if (isOverlay)
                AppendWetInk();
            else
                canvas.RaiseWetStrokeChanged();
        }

        public void End()
        {
            Settle();
            ApplyTaper();

            var points = strokePoints;
            strokePoints = null;
            wetInkDrawing.Children.Clear();
            if (!isOverlay)
                canvas.RaiseWetStrokeChanged();
            if (points is not null && points.Count > 0)
                canvas.RaiseStrokeCompleted(points);
        }

        public void Render(DrawingContext drawingContext)
        {
        }

        public Cursor GetCursor(Point canvasPoint) => Cursors.Cross;

        public void UpdateTransform(double zoom, Point origin)
            => wetInkTransform.Matrix = new Matrix(zoom, 0, 0, zoom, origin.X, origin.Y);

        public void SetStyle(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            wetInkBrush = brush;
            wetInkPen = null;
        }

        float GetPressure(float pressure) => canvas.IgnoresPressure ? SerializableStylusPoint.NeutralPressure : pressure;

        void ApplyTaper()
        {
            var taper = canvas.TaperLength;
            var points = strokePoints;
            if (points is null || taper <= 0 || points.Count < 2)
                return;

            var count = points.Count;
            if (taperDistances.Length < count)
                taperDistances = new double[count];

            taperDistances[0] = 0;
            for (var i = 1; i < count; i++)
            {
                var dx = points[i].X - points[i - 1].X;
                var dy = points[i].Y - points[i - 1].Y;
                taperDistances[i] = taperDistances[i - 1] + Math.Sqrt(dx * dx + dy * dy);
            }

            var total = taperDistances[count - 1];
            if (total <= 0)
                return;

            var limit = Math.Min(taper, total / 2);
            for (var i = 0; i < count; i++)
            {
                var head = taperDistances[i];
                var rate = Math.Min(1, Math.Min(head, total - head) / limit);
                var point = points[i];
                points[i] = new StylusPoint(point.X, point.Y, (float)(point.PressureFactor * rate));
            }
        }

        void Settle()
        {
            if (strokePoints is null || canvas.StabilizationStrength <= 0)
                return;

            while (true)
            {
                var previous = strokePoints[^1];
                var dx = rawPoint.X - previous.X;
                var dy = rawPoint.Y - previous.Y;
                if (dx * dx + dy * dy <= StabilizationSettleDistance * StabilizationSettleDistance)
                    return;

                var point = Stabilize(previous, rawPoint, previous.PressureFactor);
                if (previous.X == point.X && previous.Y == point.Y)
                    return;

                strokePoints.Add(point);
            }
        }

        StylusPoint Stabilize(StylusPoint previous, Point canvasPoint, float pressure)
        {
            var strength = canvas.StabilizationStrength;
            if (strength <= 0)
                return new StylusPoint(canvasPoint.X, canvasPoint.Y, pressure);

            var rate = 1 - strength;
            return new StylusPoint(
                previous.X + (canvasPoint.X - previous.X) * rate,
                previous.Y + (canvasPoint.Y - previous.Y) * rate,
                pressure);
        }

        void AppendWetInk()
        {
            if (strokePoints is null)
                return;

            var count = strokePoints.Count;
            if (count < 2)
                return;

            var previous = strokePoints[count - 2];
            var current = strokePoints[count - 1];
            var thickness = canvas.WetInkUsesPressure
                ? canvas.WetInkThickness * (previous.PressureFactor + current.PressureFactor)
                : canvas.WetInkThickness;

            if (count == 2)
            {
                AppendWetInkFigure(ToPoint(previous), GetMidpoint(previous, current), null, thickness);
                return;
            }

            var beforePrevious = strokePoints[count - 3];
            AppendWetInkFigure(
                GetMidpoint(beforePrevious, previous),
                GetMidpoint(previous, current),
                ToPoint(previous),
                thickness);
        }

        void AppendWetInkFigure(Point start, Point end, Point? control, double thickness)
        {
            var pen = GetWetInkPen(thickness);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                if (control is null)
                    context.LineTo(end, true, false);
                else
                    context.QuadraticBezierTo(control.Value, end, true, false);
            }
            geometry.Freeze();

            var drawing = new GeometryDrawing(null, pen, geometry);
            drawing.Freeze();
            wetInkDrawing.Children.Add(drawing);
        }

        System.Windows.Media.Pen GetWetInkPen(double thickness)
        {
            if (wetInkPen is not null && wetInkPenThickness == thickness)
                return wetInkPen;

            var pen = new System.Windows.Media.Pen(wetInkBrush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();
            wetInkPen = pen;
            wetInkPenThickness = thickness;
            return pen;
        }

        static Point ToPoint(StylusPoint point) => new(point.X, point.Y);

        static Point GetMidpoint(StylusPoint from, StylusPoint to)
            => new((from.X + to.X) / 2, (from.Y + to.Y) / 2);
    }
}
