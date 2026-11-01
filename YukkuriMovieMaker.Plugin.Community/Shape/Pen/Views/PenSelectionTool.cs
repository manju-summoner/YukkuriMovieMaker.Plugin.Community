using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    sealed class PenSelectionTool(PenEditorCanvas canvas) : IPenCanvasTool
    {
        const double SelectionGrabMargin = 4.0;
        const double HandleSize = 8.0;
        const double RotateHandleSize = 10.0;
        const double RotateHandleDistance = 22.0;
        const double MinSelectionScale = 0.01;
        const double RotationSnapAngle = 15.0;

        static readonly System.Windows.Media.Pen OutlineShadowPen = PenEditorCanvas.OutlineShadowPen;
        static readonly System.Windows.Media.Pen OutlinePen = PenEditorCanvas.OutlinePen;
        static readonly System.Windows.Media.Brush HandleBrush = PenEditorCanvas.CreateFrozenBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        static readonly System.Windows.Media.Pen HandlePen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1.0);

        readonly StreamGeometry lassoGeometry = new();

        List<Point>? lassoPoints;
        bool isMovingSelection;
        Point transformStart;
        Point transformPoint;
        Rect transformBounds;
        PenSelectionHandle activeHandle;

        public bool Begin(Point canvasPoint, float pressure)
        {
            var bounds = canvas.SelectionBounds;
            var handle = bounds.IsEmpty ? PenSelectionHandle.None : HitTestHandle(canvasPoint, bounds);
            if (handle is not PenSelectionHandle.None)
            {
                isMovingSelection = true;
                activeHandle = handle;
                transformStart = canvasPoint;
                transformPoint = canvasPoint;
                transformBounds = bounds;
                canvas.RaiseSelectionTransformStarted();
                return true;
            }

            lassoPoints = [canvasPoint];
            canvas.InvalidateVisual();
            return true;
        }

        public void Move(Point canvasPoint, float pressure)
        {
            if (isMovingSelection)
            {
                if (canvasPoint == transformPoint)
                    return;

                transformPoint = canvasPoint;
                canvas.RaiseSelectionTransformed(CreateTransform(canvasPoint));
                return;
            }

            if (lassoPoints is null)
                return;

            if (canvas.IsRectangleSelection)
            {
                if (lassoPoints.Count < 2)
                    lassoPoints.Add(canvasPoint);
                else
                    lassoPoints[1] = canvasPoint;
                canvas.InvalidateVisual();
                return;
            }

            if (lassoPoints[^1] == canvasPoint)
                return;

            lassoPoints.Add(canvasPoint);
            canvas.InvalidateVisual();
        }

        public void End()
        {
            if (isMovingSelection)
            {
                isMovingSelection = false;
                activeHandle = PenSelectionHandle.None;
                canvas.RaiseSelectionTransformCompleted();
                return;
            }

            var points = lassoPoints;
            lassoPoints = null;
            canvas.InvalidateVisual();
            if (points is null)
                return;

            if (canvas.IsRectangleSelection)
                points = CreateRectanglePolygon(points);
            canvas.RaiseLassoCompleted(points);
        }

        public void Render(DrawingContext drawingContext)
        {
            var bounds = canvas.SelectionBounds;
            if (!bounds.IsEmpty && !canvas.IsOrderMode)
            {
                var origin = canvas.CanvasToScreen(new Point(bounds.X, bounds.Y));
                var zoom = canvas.Zoom;
                var frame = new Rect(origin, new Size(bounds.Width * zoom, bounds.Height * zoom));
                drawingContext.PushGuidelineSet(CreateGuidelines(frame));
                DrawOutline(drawingContext, frame);
                DrawHandles(drawingContext, origin, frame.Width, frame.Height);
                drawingContext.Pop();
            }

            var points = lassoPoints;
            if (points is null || points.Count < 2)
                return;

            if (canvas.IsRectangleSelection)
            {
                var area = new Rect(canvas.CanvasToScreen(points[0]), canvas.CanvasToScreen(points[1]));
                drawingContext.PushGuidelineSet(CreateGuidelines(area));
                DrawOutline(drawingContext, area);
                drawingContext.Pop();
                return;
            }

            using (var context = lassoGeometry.Open())
            {
                context.BeginFigure(canvas.CanvasToScreen(points[0]), false, true);
                for (var i = 1; i < points.Count; i++)
                    context.LineTo(canvas.CanvasToScreen(points[i]), true, false);
            }
            drawingContext.DrawGeometry(null, OutlineShadowPen, lassoGeometry);
            drawingContext.DrawGeometry(null, OutlinePen, lassoGeometry);
        }

        public Cursor GetCursor(Point canvasPoint)
        {
            var bounds = canvas.SelectionBounds;
            var handle = bounds.IsEmpty ? PenSelectionHandle.None : HitTestHandle(canvasPoint, bounds);
            return GetHandleCursor(handle);
        }

        PenSelectionHandle HitTestHandle(Point canvasPoint, Rect bounds)
        {
            var half = HandleSize / 2 / canvas.Zoom;
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;

            var rotateRadius = RotateHandleSize / 2 / canvas.Zoom;
            var rotateX = canvasPoint.X - centerX;
            var rotateY = canvasPoint.Y - (bounds.Y - RotateHandleDistance / canvas.Zoom);
            if (rotateX * rotateX + rotateY * rotateY <= rotateRadius * rotateRadius)
                return PenSelectionHandle.Rotate;

            var zoom = canvas.Zoom;
            var canResizeX = bounds.Width * zoom > HandleSize;
            var canResizeY = bounds.Height * zoom > HandleSize;
            var left = canResizeX && Math.Abs(canvasPoint.X - bounds.X) <= half;
            var right = canResizeX && Math.Abs(canvasPoint.X - bounds.Right) <= half;
            var top = canResizeY && Math.Abs(canvasPoint.Y - bounds.Y) <= half;
            var bottom = canResizeY && Math.Abs(canvasPoint.Y - bounds.Bottom) <= half;
            var middleX = Math.Abs(canvasPoint.X - centerX) <= half;
            var middleY = Math.Abs(canvasPoint.Y - centerY) <= half;

            if (left && top)
                return PenSelectionHandle.TopLeft;
            if (right && top)
                return PenSelectionHandle.TopRight;
            if (left && bottom)
                return PenSelectionHandle.BottomLeft;
            if (right && bottom)
                return PenSelectionHandle.BottomRight;
            if (top && middleX)
                return PenSelectionHandle.Top;
            if (bottom && middleX)
                return PenSelectionHandle.Bottom;
            if (left && middleY)
                return PenSelectionHandle.Left;
            if (right && middleY)
                return PenSelectionHandle.Right;

            var margin = SelectionGrabMargin / canvas.Zoom;
            var inflated = bounds;
            inflated.Inflate(margin, margin);
            return inflated.Contains(canvasPoint) ? PenSelectionHandle.Move : PenSelectionHandle.None;
        }

        Matrix CreateTransform(Point canvasPoint)
        {
            var matrix = Matrix.Identity;
            if (activeHandle is PenSelectionHandle.Move)
            {
                matrix.Translate(canvasPoint.X - transformStart.X, canvasPoint.Y - transformStart.Y);
                return matrix;
            }

            var bounds = transformBounds;
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;

            var isConstrained = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (activeHandle is PenSelectionHandle.Rotate)
            {
                var from = Math.Atan2(transformStart.Y - centerY, transformStart.X - centerX);
                var to = Math.Atan2(canvasPoint.Y - centerY, canvasPoint.X - centerX);
                var angle = (to - from) * 180 / Math.PI;
                if (isConstrained)
                    angle = Math.Round(angle / RotationSnapAngle) * RotationSnapAngle;
                matrix.RotateAt(angle, centerX, centerY);
                return matrix;
            }

            GetScaleAnchor(bounds, out var anchor, out var origin);
            var scaleX = GetScale(canvasPoint.X - anchor.X, origin.X - anchor.X);
            var scaleY = GetScale(canvasPoint.Y - anchor.Y, origin.Y - anchor.Y);
            if (isConstrained && IsCornerHandle(activeHandle))
            {
                var magnitude = Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                scaleX = scaleX < 0 ? -magnitude : magnitude;
                scaleY = scaleY < 0 ? -magnitude : magnitude;
            }
            matrix.ScaleAt(scaleX, scaleY, anchor.X, anchor.Y);
            return matrix;
        }

        void GetScaleAnchor(Rect bounds, out Point anchor, out Point origin)
        {
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;
            switch (activeHandle)
            {
                case PenSelectionHandle.TopLeft:
                    anchor = new Point(bounds.Right, bounds.Bottom);
                    origin = new Point(bounds.X, bounds.Y);
                    return;
                case PenSelectionHandle.TopRight:
                    anchor = new Point(bounds.X, bounds.Bottom);
                    origin = new Point(bounds.Right, bounds.Y);
                    return;
                case PenSelectionHandle.BottomLeft:
                    anchor = new Point(bounds.Right, bounds.Y);
                    origin = new Point(bounds.X, bounds.Bottom);
                    return;
                case PenSelectionHandle.BottomRight:
                    anchor = new Point(bounds.X, bounds.Y);
                    origin = new Point(bounds.Right, bounds.Bottom);
                    return;
                case PenSelectionHandle.Top:
                    anchor = new Point(centerX, bounds.Bottom);
                    origin = new Point(centerX, bounds.Y);
                    return;
                case PenSelectionHandle.Bottom:
                    anchor = new Point(centerX, bounds.Y);
                    origin = new Point(centerX, bounds.Bottom);
                    return;
                case PenSelectionHandle.Left:
                    anchor = new Point(bounds.Right, centerY);
                    origin = new Point(bounds.X, centerY);
                    return;
                default:
                    anchor = new Point(bounds.X, centerY);
                    origin = new Point(bounds.Right, centerY);
                    return;
            }
        }

        static bool IsCornerHandle(PenSelectionHandle handle)
            => handle is PenSelectionHandle.TopLeft or PenSelectionHandle.TopRight
                or PenSelectionHandle.BottomLeft or PenSelectionHandle.BottomRight;

        static double GetScale(double moved, double original)
        {
            if (original == 0)
                return 1;

            var scale = moved / original;
            if (scale >= MinSelectionScale || scale <= -MinSelectionScale)
                return scale;
            return scale < 0 ? -MinSelectionScale : MinSelectionScale;
        }

        static List<Point> CreateRectanglePolygon(List<Point> points)
        {
            if (points.Count < 2)
                return points;

            var start = points[0];
            var end = points[1];
            return [start, new Point(end.X, start.Y), end, new Point(start.X, end.Y)];
        }

        static Cursor GetHandleCursor(PenSelectionHandle handle) => handle switch
        {
            PenSelectionHandle.TopLeft or PenSelectionHandle.BottomRight => Cursors.SizeNWSE,
            PenSelectionHandle.TopRight or PenSelectionHandle.BottomLeft => Cursors.SizeNESW,
            PenSelectionHandle.Top or PenSelectionHandle.Bottom => Cursors.SizeNS,
            PenSelectionHandle.Left or PenSelectionHandle.Right => Cursors.SizeWE,
            PenSelectionHandle.Move => Cursors.SizeAll,
            PenSelectionHandle.Rotate => Cursors.Hand,
            _ => Cursors.Cross,
        };

        static GuidelineSet CreateGuidelines(Rect rect)
        {
            var half = OutlinePen.Thickness / 2;
            var guidelines = new GuidelineSet();
            guidelines.GuidelinesX.Add(rect.Left + half);
            guidelines.GuidelinesX.Add(rect.Left + rect.Width / 2 + half);
            guidelines.GuidelinesX.Add(rect.Right + half);
            guidelines.GuidelinesY.Add(rect.Top + half);
            guidelines.GuidelinesY.Add(rect.Top + rect.Height / 2 + half);
            guidelines.GuidelinesY.Add(rect.Bottom + half);
            return guidelines;
        }

        static void DrawOutline(DrawingContext drawingContext, Rect rect)
        {
            drawingContext.DrawRectangle(null, OutlineShadowPen, rect);
            drawingContext.DrawRectangle(null, OutlinePen, rect);
        }

        static void DrawHandles(DrawingContext drawingContext, Point origin, double width, double height)
        {
            var centerX = origin.X + width / 2;
            var rotate = new Point(centerX, origin.Y - RotateHandleDistance);
            drawingContext.DrawLine(OutlineShadowPen, new Point(centerX, origin.Y), rotate);
            drawingContext.DrawLine(OutlinePen, new Point(centerX, origin.Y), rotate);
            drawingContext.DrawEllipse(HandleBrush, HandlePen, rotate, RotateHandleSize / 2, RotateHandleSize / 2);

            var canResizeX = width > HandleSize;
            var canResizeY = height > HandleSize;
            var centerY = origin.Y + height / 2;
            var right = origin.X + width;
            var bottom = origin.Y + height;
            if (canResizeX && canResizeY)
            {
                DrawHandle(drawingContext, new Point(origin.X, origin.Y));
                DrawHandle(drawingContext, new Point(right, origin.Y));
                DrawHandle(drawingContext, new Point(right, bottom));
                DrawHandle(drawingContext, new Point(origin.X, bottom));
            }
            if (canResizeY)
            {
                DrawHandle(drawingContext, new Point(centerX, origin.Y));
                DrawHandle(drawingContext, new Point(centerX, bottom));
            }
            if (canResizeX)
            {
                DrawHandle(drawingContext, new Point(right, centerY));
                DrawHandle(drawingContext, new Point(origin.X, centerY));
            }
        }

        static void DrawHandle(DrawingContext drawingContext, Point center)
        {
            var half = HandleSize / 2;
            drawingContext.DrawRectangle(HandleBrush, HandlePen, new Rect(center.X - half, center.Y - half, HandleSize, HandleSize));
        }
    }
}
