using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    sealed class PenOrderTool : IPenCanvasTool
    {
        const double BadgeRadius = 9.0;
        const double BadgeRingRadius = 12.0;
        const double BadgeFontSize = 11.0;
        const double BadgeStackStep = BadgeRadius * 2 + 2;

        static readonly System.Windows.Media.Pen OutlineShadowPen = PenEditorCanvas.OutlineShadowPen;
        static readonly System.Windows.Media.Pen OutlinePen = PenEditorCanvas.OutlinePen;
        static readonly System.Windows.Media.Brush BadgeBrush = PenEditorCanvas.CreateFrozenBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        static readonly System.Windows.Media.Brush BadgeSelectedBrush = PenEditorCanvas.CreateFrozenBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        static readonly System.Windows.Media.Brush BadgeDimBrush = PenEditorCanvas.CreateFrozenBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
        static readonly System.Windows.Media.Brush BadgeDimSelectedBrush = PenEditorCanvas.CreateFrozenBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00));
        static readonly System.Windows.Media.Pen BadgePen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1.0);
        static readonly System.Windows.Media.Pen BadgeSelectedPen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen BadgeDimPen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0x60, 0x00, 0x00, 0x00), 1.0);
        static readonly System.Windows.Media.Pen BadgeDimSelectedPen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen BadgeDropPen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 2.0);
        static readonly System.Windows.Media.Pen BadgeDropShadowPen = PenEditorCanvas.CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 4.0);
        static readonly Typeface BadgeTypeface = new(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        readonly PenEditorCanvas canvas;
        readonly Dictionary<int, Geometry> badgeGeometries = [];
        readonly List<PenOrderBadgeVisual> badgeVisuals = [];
        readonly List<int> badgeSignatures = [];
        readonly List<Point> badgeCenters = [];
        readonly PenOrderBadgeVisual dropVisual = new();

        double pixelsPerDip = 1;
        bool isDragMoved;
        int dragLayerIndex;
        int dropBadgeIndex = -1;
        Point pressPoint;
        Point pointer;

        public PenOrderTool(PenEditorCanvas canvas)
        {
            this.canvas = canvas;
            dropVisual.Opacity = 0;
            using var context = dropVisual.RenderOpen();
            context.DrawEllipse(null, BadgeDropShadowPen, default, BadgeRingRadius, BadgeRingRadius);
            context.DrawEllipse(null, BadgeDropPen, default, BadgeRingRadius, BadgeRingRadius);
        }

        public int VisualCount => badgeVisuals.Count + 1;

        public Visual GetVisual(int index)
            => index < badgeVisuals.Count ? badgeVisuals[index] : dropVisual;

        public bool Begin(Point canvasPoint, float pressure)
        {
            var index = HitTestBadge(canvasPoint);
            if (index < 0)
            {
                canvas.RaiseOrderBackgroundPressed();
                return false;
            }

            var badges = canvas.OrderBadges!;
            var badge = badges[index];
            var isToggle = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            canvas.RaiseOrderBadgePressed(new PenOrderBadgeEventArgs(badge.LayerIndex, badge.StrokeIndex, isToggle));

            index = HitTestBadge(canvasPoint);
            if (index < 0)
                return false;
            badge = canvas.OrderBadges![index];
            if (!badge.IsMovable || !badge.IsSelected)
                return false;

            isDragMoved = false;
            dragLayerIndex = badge.LayerIndex;
            dropBadgeIndex = -1;
            pressPoint = canvasPoint;
            pointer = canvasPoint;
            return true;
        }

        public void Move(Point canvasPoint, float pressure)
        {
            pointer = canvasPoint;
            if (!isDragMoved)
            {
                var zoom = canvas.Zoom;
                var dx = (canvasPoint.X - pressPoint.X) * zoom;
                var dy = (canvasPoint.Y - pressPoint.Y) * zoom;
                if (dx * dx + dy * dy < PenEditorCanvas.DragThreshold * PenEditorCanvas.DragThreshold)
                    return;
                isDragMoved = true;
            }

            var index = HitTestBadge(canvasPoint);
            if (index >= 0)
            {
                var badge = canvas.OrderBadges![index];
                if (badge.IsSelected || badge.LayerIndex != dragLayerIndex)
                    index = -1;
            }
            dropBadgeIndex = index;
            UpdateDropVisual();
            canvas.InvalidateVisual();
        }

        public void End()
        {
            var index = isDragMoved ? dropBadgeIndex : -1;
            isDragMoved = false;
            dropBadgeIndex = -1;
            UpdateDropVisual();
            canvas.InvalidateVisual();

            var badges = canvas.OrderBadges;
            if (badges is null || index < 0 || index >= badges.Length)
                return;
            var badge = badges[index];
            canvas.RaiseOrderBadgeDropped(new PenOrderBadgeEventArgs(badge.LayerIndex, badge.StrokeIndex, false));
        }

        public void Render(DrawingContext drawingContext)
        {
            var badges = canvas.OrderBadges;
            if (!canvas.IsOrderMode || badges is null || !isDragMoved)
                return;

            var screen = canvas.CanvasToScreen(pointer);
            var count = Math.Min(badges.Length, badgeCenters.Count);
            for (var i = 0; i < count; i++)
            {
                var badge = badges[i];
                if (!badge.IsSelected || badge.LayerIndex != dragLayerIndex)
                    continue;
                drawingContext.DrawLine(OutlineShadowPen, badgeCenters[i], screen);
                drawingContext.DrawLine(OutlinePen, badgeCenters[i], screen);
            }
        }

        public Cursor GetCursor(Point canvasPoint)
            => HitTestBadge(canvasPoint) >= 0 ? Cursors.Hand : Cursors.Arrow;

        public void SetPixelsPerDip(double value)
        {
            if (pixelsPerDip == value)
                return;
            pixelsPerDip = value;
            badgeGeometries.Clear();
            for (var i = 0; i < badgeSignatures.Count; i++)
                badgeSignatures[i] = -1;
            Sync();
        }

        public void Sync()
        {
            var badges = canvas.IsOrderMode ? canvas.OrderBadges : null;
            var count = badges?.Length ?? 0;

            while (badgeVisuals.Count > count)
            {
                var last = badgeVisuals.Count - 1;
                canvas.DetachVisual(badgeVisuals[last]);
                badgeVisuals.RemoveAt(last);
                badgeSignatures.RemoveAt(last);
                badgeCenters.RemoveAt(last);
            }
            while (badgeVisuals.Count < count)
            {
                var visual = new PenOrderBadgeVisual();
                canvas.AttachVisual(visual);
                badgeVisuals.Add(visual);
                badgeSignatures.Add(-1);
                badgeCenters.Add(default);
            }

            for (var i = 0; i < count; i++)
            {
                var badge = badges![i];
                var signature = (badge.Number << 3) | (badge.IsSelected ? 4 : 0) | (badge.IsDrawn ? 2 : 0) | (badge.IsIndependent ? 1 : 0);
                if (badgeSignatures[i] != signature)
                {
                    RenderBadge(badgeVisuals[i], badge);
                    badgeSignatures[i] = signature;
                }
            }
            UpdatePositions();
            if (dropBadgeIndex >= count)
                dropBadgeIndex = -1;
            UpdateDropVisual();
        }

        public void UpdatePositions()
        {
            var badges = canvas.OrderBadges;
            if (badges is null)
                return;

            var count = Math.Min(badges.Length, badgeVisuals.Count);
            for (var i = 0; i < count; i++)
            {
                var center = ResolveBadgeStack(canvas.CanvasToScreen(badges[i].Start), i);
                badgeCenters[i] = center;
                badgeVisuals[i].MoveTo(center);
            }
            UpdateDropVisual();
        }

        int HitTestBadge(Point canvasPoint)
        {
            var screen = canvas.CanvasToScreen(canvasPoint);
            for (var i = badgeCenters.Count - 1; i >= 0; i--)
            {
                var center = badgeCenters[i];
                var dx = center.X - screen.X;
                var dy = center.Y - screen.Y;
                if (dx * dx + dy * dy <= BadgeRadius * BadgeRadius)
                    return i;
            }
            return -1;
        }

        void RenderBadge(PenOrderBadgeVisual visual, in PenOrderBadge badge)
        {
            var fill = badge.IsDrawn
                ? (badge.IsSelected ? BadgeSelectedBrush : BadgeBrush)
                : (badge.IsSelected ? BadgeDimSelectedBrush : BadgeDimBrush);
            var pen = badge.IsDrawn
                ? (badge.IsSelected ? BadgeSelectedPen : BadgePen)
                : (badge.IsSelected ? BadgeDimSelectedPen : BadgeDimPen);
            var textBrush = badge.IsDrawn
                ? (badge.IsSelected ? BadgeBrush : BadgeSelectedBrush)
                : (badge.IsSelected ? BadgeDimBrush : BadgeDimSelectedBrush);

            using var context = visual.RenderOpen();
            if (badge.IsIndependent)
            {
                context.DrawEllipse(null, OutlineShadowPen, default, BadgeRingRadius, BadgeRingRadius);
                context.DrawEllipse(null, OutlinePen, default, BadgeRingRadius, BadgeRingRadius);
            }
            context.DrawEllipse(fill, pen, default, BadgeRadius, BadgeRadius);
            context.DrawGeometry(textBrush, null, GetBadgeGeometry(badge.Number));
        }

        Geometry GetBadgeGeometry(int number)
        {
            if (badgeGeometries.TryGetValue(number, out var geometry))
                return geometry;

            var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeTypeface, BadgeFontSize, BadgeSelectedBrush, pixelsPerDip);
            geometry = text.BuildGeometry(new Point(-text.Width / 2, -text.Height / 2));
            geometry.Freeze();
            badgeGeometries.Add(number, geometry);
            return geometry;
        }

        Point ResolveBadgeStack(Point center, int index)
        {
            var i = 0;
            while (i < index)
            {
                var other = badgeCenters[i];
                var dx = center.X - other.X;
                var dy = center.Y - other.Y;
                if (dx * dx + dy * dy < 1)
                {
                    center = new Point(other.X + BadgeStackStep, other.Y);
                    i = 0;
                    continue;
                }
                i++;
            }
            return center;
        }

        void UpdateDropVisual()
        {
            if (dropBadgeIndex < 0 || dropBadgeIndex >= badgeCenters.Count)
            {
                dropVisual.Opacity = 0;
                return;
            }

            dropVisual.MoveTo(badgeCenters[dropBadgeIndex]);
            dropVisual.Opacity = 1;
        }
    }
}
