using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.VectorFieldWarp
{
    internal sealed class VectorFieldPointCanvas : FrameworkElement
    {
        const double PointRadius = 5.0;
        const double PointHitRadius = 9.0;
        const double HoverRadiusBonus = 2.0;
        const double MinZoom = 0.25;
        const double MaxZoom = 16.0;
        const double ZoomStep = 1.2;
        const double PanThreshold = 3.0;
        const double ScrollBarThickness = 17.0;
        const double ScrollClearMargin = PointHitRadius + 4;
        const double FieldArrowSpacing = 32.0;
        const double FieldArrowLength = 11.0;
        const double FieldArrowHeadSize = 3.5;
        const double FieldArrowMinVelocity = 1e-3;

        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Brush PointFillBrush = CreateFrozenBrush(Color.FromRgb(0x2E, 0x86, 0xFF));
        static readonly System.Windows.Media.Brush DisabledPointFillBrush = CreateFrozenBrush(Color.FromArgb(0xA0, 0x80, 0x80, 0x80));
        static readonly Pen PointStrokePen = CreateFrozenPen(Colors.White, 1.5);
        static readonly Pen PointHaloPen = CreateFrozenPen(Color.FromArgb(0x80, 0x00, 0x00, 0x00), 3.5);
        static readonly Pen PointSelectionPen = CreateFrozenPen(Color.FromRgb(0x2E, 0x86, 0xFF), 2.0);
        static readonly Pen RadiusPen = CreateFrozenDashedPen(Color.FromArgb(0x60, 0x2E, 0x86, 0xFF), 1.0);
        static readonly Pen SelectedRadiusPen = CreateFrozenDashedPen(Color.FromArgb(0xC0, 0x2E, 0x86, 0xFF), 1.5);
        static readonly System.Windows.Media.Brush LabelBrush = CreateFrozenBrush(Colors.White);
        static readonly Pen FieldArrowPen = CreateFrozenPen(Color.FromArgb(0xC8, 0x4F, 0xD8, 0x8A), 1.2);

        VectorFieldPointListEditorViewModel? viewModel;
        ImmutableList<VectorFieldPointItemViewModel> points = [];

        bool isDragging;
        bool dragMoved;
        Point lastDragPosition;

        VectorFieldPointItemViewModel? hoveredPoint;

        double zoom = 1.0;
        Size lastImageSize = Size.Empty;
        bool pendingResetView = true;

        bool isPanning;
        bool panMoved;
        Point lastPanPosition;

        public double ScrollOffsetX
        {
            get => (double)GetValue(ScrollOffsetXProperty);
            set => SetValue(ScrollOffsetXProperty, value);
        }
        public static readonly DependencyProperty ScrollOffsetXProperty =
            DependencyProperty.Register(nameof(ScrollOffsetX), typeof(double), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollOffsetChanged, CoerceScrollOffsetX));

        public double ScrollOffsetY
        {
            get => (double)GetValue(ScrollOffsetYProperty);
            set => SetValue(ScrollOffsetYProperty, value);
        }
        public static readonly DependencyProperty ScrollOffsetYProperty =
            DependencyProperty.Register(nameof(ScrollOffsetY), typeof(double), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollOffsetChanged, CoerceScrollOffsetY));

        public double ScrollMaxX
        {
            get => (double)GetValue(ScrollMaxXProperty);
            private set => SetValue(ScrollMaxXPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollMaxXPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollMaxX), typeof(double), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollMaxXChanged));
        public static readonly DependencyProperty ScrollMaxXProperty = ScrollMaxXPropertyKey.DependencyProperty;

        public double ScrollMaxY
        {
            get => (double)GetValue(ScrollMaxYProperty);
            private set => SetValue(ScrollMaxYPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollMaxYPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollMaxY), typeof(double), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(0.0, OnScrollMaxYChanged));
        public static readonly DependencyProperty ScrollMaxYProperty = ScrollMaxYPropertyKey.DependencyProperty;

        public double ScrollViewportW
        {
            get => (double)GetValue(ScrollViewportWProperty);
            private set => SetValue(ScrollViewportWPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollViewportWPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollViewportW), typeof(double), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(0.0));
        public static readonly DependencyProperty ScrollViewportWProperty = ScrollViewportWPropertyKey.DependencyProperty;

        public double ScrollViewportH
        {
            get => (double)GetValue(ScrollViewportHProperty);
            private set => SetValue(ScrollViewportHPropertyKey, value);
        }
        static readonly DependencyPropertyKey ScrollViewportHPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ScrollViewportH), typeof(double), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(0.0));
        public static readonly DependencyProperty ScrollViewportHProperty = ScrollViewportHPropertyKey.DependencyProperty;

        public Visibility HScrollVisibility
        {
            get => (Visibility)GetValue(HScrollVisibilityProperty);
            private set => SetValue(HScrollVisibilityPropertyKey, value);
        }
        static readonly DependencyPropertyKey HScrollVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HScrollVisibility), typeof(Visibility), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty HScrollVisibilityProperty = HScrollVisibilityPropertyKey.DependencyProperty;

        public Visibility VScrollVisibility
        {
            get => (Visibility)GetValue(VScrollVisibilityProperty);
            private set => SetValue(VScrollVisibilityPropertyKey, value);
        }
        static readonly DependencyPropertyKey VScrollVisibilityPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(VScrollVisibility), typeof(Visibility), typeof(VectorFieldPointCanvas),
                new FrameworkPropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty VScrollVisibilityProperty = VScrollVisibilityPropertyKey.DependencyProperty;

        static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((VectorFieldPointCanvas)d).InvalidateVisual();

        static object CoerceScrollOffsetX(DependencyObject d, object baseValue)
            => Math.Clamp((double)baseValue, 0, ((VectorFieldPointCanvas)d).ScrollMaxX);

        static object CoerceScrollOffsetY(DependencyObject d, object baseValue)
            => Math.Clamp((double)baseValue, 0, ((VectorFieldPointCanvas)d).ScrollMaxY);

        static void OnScrollMaxXChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => d.CoerceValue(ScrollOffsetXProperty);

        static void OnScrollMaxYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => d.CoerceValue(ScrollOffsetYProperty);

        public VectorFieldPointCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            System.Windows.Controls.ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            DataContextChanged += OnDataContextChanged;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                DetachPoints();
            }
            viewModel = e.NewValue as VectorFieldPointListEditorViewModel;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                AttachPoints();
            }
            SyncViewToCurrentImage();
        }

        void SyncViewToCurrentImage()
        {
            var image = viewModel?.CanvasImage;
            var size = image is null ? Size.Empty : new Size(image.PixelWidth, image.PixelHeight);
            if (size != lastImageSize)
            {
                lastImageSize = size;
                ResetView();
            }
            else
            {
                UpdateScrollInfo();
                InvalidateVisual();
            }
        }

        void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VectorFieldPointListEditorViewModel.CanvasImage))
            {
                SyncViewToCurrentImage();
            }
            else if (e.PropertyName == nameof(VectorFieldPointListEditorViewModel.CanvasPoints))
            {
                DetachPoints();
                AttachPoints();
                ClearHover();
                UpdateScrollInfo();
                InvalidateVisual();
            }
        }

        void AttachPoints()
        {
            points = viewModel?.CanvasPoints ?? [];
            foreach (var point in points)
            {
                point.PropertyChanged += Point_PropertyChanged;
                point.VisualChanged += Point_VisualChanged;
            }
        }

        void DetachPoints()
        {
            foreach (var point in points)
            {
                point.PropertyChanged -= Point_PropertyChanged;
                point.VisualChanged -= Point_VisualChanged;
            }
            points = [];
        }

        void Point_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(VectorFieldPointItemViewModel.IsSelected) or nameof(VectorFieldPointItemViewModel.IsEnabled))
                InvalidateVisual();
        }

        void Point_VisualChanged(object? sender, EventArgs e)
        {
            UpdateScrollInfo();
            InvalidateVisual();
        }

        readonly record struct ViewMetrics(
            double Scale, Rect ContentLocal, double ImageWidth, double ImageHeight,
            double ViewportW, double ViewportH, double ExtentW, double ExtentH, bool NeedH, bool NeedV);

        readonly record struct CanvasLayout(double Scale, double ImageScale, Point Origin, double ImageWidth, double ImageHeight);

        double GetImageScale() => Math.Max(viewModel?.CanvasImageScale ?? 1.0, 1e-6);

        ViewMetrics? GetMetrics()
        {
            var image = viewModel?.CanvasImage;
            if (image is null || RenderSize.Width <= 0 || RenderSize.Height <= 0)
                return null;

            double iw = image.PixelWidth;
            double ih = image.PixelHeight;
            if (iw <= 0 || ih <= 0)
                return null;

            double fullW = RenderSize.Width, fullH = RenderSize.Height;
            var fitScale = Math.Min(fullW / iw, fullH / ih);
            var scale = fitScale * zoom;

            var content = GetContentLocalBounds(iw, ih, GetImageScale());
            double extentW = content.Width * scale;
            double extentH = content.Height * scale;

            const double reserve = ScrollBarThickness + ScrollClearMargin;
            bool needV = extentH > fullH + 0.5;
            bool needH = extentW > fullW + 0.5;
            if (needV && !needH) needH = extentW > fullW - reserve + 0.5;
            if (needH && !needV) needV = extentH > fullH - reserve + 0.5;
            double effW = fullW - (needV ? reserve : 0);
            double effH = fullH - (needH ? reserve : 0);

            return new ViewMetrics(scale, content, iw, ih, effW, effH, extentW, extentH, needH, needV);
        }

        Rect GetContentLocalBounds(double iw, double ih, double imageScale)
        {
            double minX = -iw * 0.5, minY = -ih * 0.5, maxX = iw * 0.5, maxY = ih * 0.5;
            foreach (var point in points)
            {
                var p = GetPointPosition(point);
                var x = p.X * imageScale;
                var y = p.Y * imageScale;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }

        CanvasLayout? GetLayout()
        {
            var metrics = GetMetrics();
            if (metrics is null)
                return null;
            var m = metrics.Value;

            double padX = Math.Max(0, (m.ViewportW - m.ExtentW) * 0.5);
            double padY = Math.Max(0, (m.ViewportH - m.ExtentH) * 0.5);
            double offX = Math.Clamp(ScrollOffsetX, 0, Math.Max(0, m.ExtentW - m.ViewportW));
            double offY = Math.Clamp(ScrollOffsetY, 0, Math.Max(0, m.ExtentH - m.ViewportH));

            double originX = padX - offX - (m.ContentLocal.Left + m.ImageWidth * 0.5) * m.Scale;
            double originY = padY - offY - (m.ContentLocal.Top + m.ImageHeight * 0.5) * m.Scale;
            return new CanvasLayout(m.Scale, GetImageScale(), new Point(originX, originY), m.ImageWidth, m.ImageHeight);
        }

        void UpdateScrollInfo()
        {
            var metrics = GetMetrics();
            if (metrics is null)
            {
                ScrollMaxX = 0;
                ScrollMaxY = 0;
                ScrollViewportW = 0;
                ScrollViewportH = 0;
                HScrollVisibility = Visibility.Collapsed;
                VScrollVisibility = Visibility.Collapsed;
                return;
            }
            var m = metrics.Value;
            double maxX = Math.Max(0, m.ExtentW - m.ViewportW);
            double maxY = Math.Max(0, m.ExtentH - m.ViewportH);

            ScrollMaxX = maxX;
            ScrollMaxY = maxY;
            ScrollViewportW = m.ViewportW;
            ScrollViewportH = m.ViewportH;
            HScrollVisibility = m.NeedH ? Visibility.Visible : Visibility.Collapsed;
            VScrollVisibility = m.NeedV ? Visibility.Visible : Visibility.Collapsed;

            if (pendingResetView)
            {
                double padX = Math.Max(0, (m.ViewportW - m.ExtentW) * 0.5);
                double padY = Math.Max(0, (m.ViewportH - m.ExtentH) * 0.5);
                double centerOffX = padX - m.ContentLocal.Left * m.Scale - m.ViewportW * 0.5;
                double centerOffY = padY - m.ContentLocal.Top * m.Scale - m.ViewportH * 0.5;
                SetCurrentValue(ScrollOffsetXProperty, Math.Clamp(centerOffX, 0, maxX));
                SetCurrentValue(ScrollOffsetYProperty, Math.Clamp(centerOffY, 0, maxY));
                pendingResetView = false;
            }
        }

        public void ResetView()
        {
            zoom = 1.0;
            pendingResetView = true;
            UpdateScrollInfo();
            InvalidateVisual();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateScrollInfo();
            InvalidateVisual();
        }

        static Point DisplayToLocal(Point display, CanvasLayout layout)
            => new(
                ((display.X - layout.Origin.X) / layout.Scale - layout.ImageWidth * 0.5) / layout.ImageScale,
                ((display.Y - layout.Origin.Y) / layout.Scale - layout.ImageHeight * 0.5) / layout.ImageScale);

        static Point LocalToDisplay(Point local, CanvasLayout layout)
            => new(
                layout.Origin.X + (local.X * layout.ImageScale + layout.ImageWidth * 0.5) * layout.Scale,
                layout.Origin.Y + (local.Y * layout.ImageScale + layout.ImageHeight * 0.5) * layout.Scale);

        static Point GetPointPosition(VectorFieldPointItemViewModel point)
            => new(
                point.Model.X.Values.FirstOrDefault()?.Value ?? 0,
                point.Model.Y.Values.FirstOrDefault()?.Value ?? 0);

        readonly record struct FieldSource(double X, double Y, double Radial, double Vortex, double Radius);

        FieldSource[] GetFieldSources()
            => [.. points
                .Where(p => p.IsEnabled)
                .Select(p => new FieldSource(
                    p.Model.X.Values.FirstOrDefault()?.Value ?? 0,
                    p.Model.Y.Values.FirstOrDefault()?.Value ?? 0,
                    p.Model.RadialStrength.Values.FirstOrDefault()?.Value ?? 0,
                    p.Model.VortexStrength.Values.FirstOrDefault()?.Value ?? 0,
                    Math.Max(p.Model.Radius.Values.FirstOrDefault()?.Value ?? 1, 1)))
                .Where(s => s.Radial != 0 || s.Vortex != 0)];

        static Vector EvaluateField(FieldSource[] sources, Point local)
        {
            var velocity = new Vector();
            foreach (var source in sources)
            {
                var dx = local.X - source.X;
                var dy = local.Y - source.Y;
                var denominator = Math.Max(dx * dx + dy * dy + source.Radius * source.Radius, 1e-6);
                var factor = source.Radius / denominator;
                velocity.X += factor * (source.Radial * dx + source.Vortex * -dy);
                velocity.Y += factor * (source.Radial * dy + source.Vortex * dx);
            }
            return velocity;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var bounds = new Rect(RenderSize);
            drawingContext.DrawRectangle(CheckerBrush, null, bounds);

            var layout = GetLayout();
            if (layout is null)
            {
                DrawCenteredText(drawingContext, Texts.VectorFieldWarpCanvasNoImage, bounds);
                return;
            }
            var l = layout.Value;

            var image = viewModel!.CanvasImage!;
            drawingContext.DrawImage(image, new Rect(l.Origin, new Size(l.ImageWidth * l.Scale, l.ImageHeight * l.Scale)));

            DrawFieldArrows(drawingContext, l, bounds);
            DrawPoints(drawingContext, l);
        }

        void DrawFieldArrows(DrawingContext drawingContext, CanvasLayout layout, Rect bounds)
        {
            var sources = GetFieldSources();
            if (sources.Length == 0)
                return;

            var maxSpeed = sources.Max(s => (Math.Abs(s.Radial) + Math.Abs(s.Vortex)) * 0.5);
            if (maxSpeed <= FieldArrowMinVelocity)
                return;

            var offsetX = ((layout.Origin.X % FieldArrowSpacing) + FieldArrowSpacing) % FieldArrowSpacing;
            var offsetY = ((layout.Origin.Y % FieldArrowSpacing) + FieldArrowSpacing) % FieldArrowSpacing;
            for (var y = offsetY; y < bounds.Height; y += FieldArrowSpacing)
            {
                for (var x = offsetX; x < bounds.Width; x += FieldArrowSpacing)
                {
                    var display = new Point(x, y);
                    var local = DisplayToLocal(display, layout);
                    var velocity = EvaluateField(sources, local);
                    var speed = velocity.Length;
                    if (speed <= FieldArrowMinVelocity)
                        continue;

                    var intensity = Math.Clamp(speed / maxSpeed, 0.08, 1.0);
                    var direction = velocity / speed;
                    var length = FieldArrowLength * (0.35 + 0.65 * intensity);
                    var tip = display + direction * (length * 0.5);
                    var tail = display - direction * (length * 0.5);
                    var normal = new Vector(-direction.Y, direction.X);
                    var left = tip - direction * FieldArrowHeadSize + normal * (FieldArrowHeadSize * 0.6);
                    var right = tip - direction * FieldArrowHeadSize - normal * (FieldArrowHeadSize * 0.6);

                    drawingContext.PushOpacity(intensity);
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(tail, false, false);
                        ctx.LineTo(tip, true, true);
                        ctx.LineTo(left, true, true);
                        ctx.BeginFigure(tip, false, false);
                        ctx.LineTo(right, true, true);
                    }
                    geometry.Freeze();
                    drawingContext.DrawGeometry(null, FieldArrowPen, geometry);
                    drawingContext.Pop();
                }
            }
        }

        void DrawPoints(DrawingContext drawingContext, CanvasLayout layout)
        {
            foreach (var point in points)
            {
                var p = LocalToDisplay(GetPointPosition(point), layout);
                var displayRadius = Math.Max(point.Model.Radius.Values.FirstOrDefault()?.Value ?? 1, 1) * layout.Scale * layout.ImageScale;
                drawingContext.DrawEllipse(null, point.IsSelected ? SelectedRadiusPen : RadiusPen, p, displayRadius, displayRadius);
            }

            foreach (var point in points)
            {
                var p = LocalToDisplay(GetPointPosition(point), layout);
                var radius = point == hoveredPoint ? PointRadius + HoverRadiusBonus : PointRadius;

                if (point.IsSelected)
                    drawingContext.DrawEllipse(null, PointSelectionPen, p, radius + 3.5, radius + 3.5);

                drawingContext.DrawEllipse(null, PointHaloPen, p, radius, radius);
                drawingContext.DrawEllipse(point.IsEnabled ? PointFillBrush : DisabledPointFillBrush, PointStrokePen, p, radius, radius);
            }
        }

        void DrawCenteredText(DrawingContext drawingContext, string text, Rect bounds)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11,
                LabelBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, bounds.Width - 8)
            };
            drawingContext.DrawText(
                formatted,
                new Point(
                    bounds.Left + (bounds.Width - Math.Min(formatted.Width, formatted.MaxTextWidth)) * 0.5,
                    bounds.Top + (bounds.Height - formatted.Height) * 0.5));
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;
            if (e.Delta == 0)
                return;

            var layout = GetLayout();
            if (layout is null)
                return;

            var cursor = e.GetPosition(this);
            var anchor = DisplayToLocal(cursor, layout.Value);

            var newZoom = Math.Clamp(zoom * (e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep), MinZoom, MaxZoom);
            if (newZoom == zoom)
            {
                e.Handled = true;
                return;
            }
            zoom = newZoom;
            UpdateScrollInfo();

            var layout2 = GetLayout();
            if (layout2 is not null)
            {
                var after = LocalToDisplay(anchor, layout2.Value);
                SetCurrentValue(ScrollOffsetXProperty, Math.Clamp(ScrollOffsetX + (after.X - cursor.X), 0, ScrollMaxX));
                SetCurrentValue(ScrollOffsetYProperty, Math.Clamp(ScrollOffsetY + (after.Y - cursor.Y), 0, ScrollMaxY));
            }
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            if (viewModel is null)
                return;
            var layout = GetLayout();
            if (layout is null)
                return;

            var pos = e.GetPosition(this);

            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                BeginPan(pos, e);
                return;
            }

            var hit = HitTestPoint(pos, layout.Value);
            if (hit is not null)
            {
                viewModel.SelectFromCanvas(hit, (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            }
            else
            {
                if (!viewModel.CanAddPoint)
                    return;
                var local = DisplayToLocal(pos, layout.Value);
                viewModel.AddPointFromCanvas(local.X, local.Y);

                hit = HitTestPoint(pos, layout.Value);
                if (hit is null)
                    return;
            }

            isDragging = true;
            dragMoved = false;
            lastDragPosition = pos;
            CaptureMouse();
            e.Handled = true;
        }

        void BeginPan(Point pos, MouseButtonEventArgs e)
        {
            if (isDragging)
                return;
            isPanning = true;
            panMoved = false;
            lastPanPosition = pos;
            Focus();
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isPanning)
            {
                var p = e.GetPosition(this);
                var dxp = p.X - lastPanPosition.X;
                var dyp = p.Y - lastPanPosition.Y;
                if (!panMoved && Math.Abs(dxp) < PanThreshold && Math.Abs(dyp) < PanThreshold)
                    return;
                if (!panMoved)
                    ClearHover();
                panMoved = true;
                SetCurrentValue(ScrollOffsetXProperty, Math.Clamp(ScrollOffsetX - dxp, 0, ScrollMaxX));
                SetCurrentValue(ScrollOffsetYProperty, Math.Clamp(ScrollOffsetY - dyp, 0, ScrollMaxY));
                lastPanPosition = p;
                InvalidateVisual();
                return;
            }

            if (viewModel is null)
                return;
            var layout = GetLayout();
            if (layout is null || layout.Value.Scale <= 0)
                return;

            var pos = e.GetPosition(this);

            if (!isDragging)
            {
                UpdateHover(pos, layout.Value);
                return;
            }
            var dx = (pos.X - lastDragPosition.X) / (layout.Value.Scale * layout.Value.ImageScale);
            var dy = (pos.Y - lastDragPosition.Y) / (layout.Value.Scale * layout.Value.ImageScale);
            if (dx == 0 && dy == 0)
                return;

            if (!dragMoved)
            {
                viewModel.BeginDragFromCanvas();
                dragMoved = true;
            }
            viewModel.MoveSelectedFromCanvas(dx, dy);
            lastDragPosition = pos;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (isPanning)
            {
                isPanning = false;
                panMoved = false;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }
            if (!isDragging)
                return;
            isDragging = false;
            ReleaseMouseCapture();
            EndActiveDrag();
            e.Handled = true;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (isPanning)
            {
                isPanning = false;
                panMoved = false;
            }
            if (!isDragging)
                return;
            isDragging = false;
            EndActiveDrag();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            ClearHover();
        }

        void UpdateHover(Point pos, CanvasLayout layout)
        {
            var point = HitTestPoint(pos, layout);
            if (point == hoveredPoint)
                return;
            hoveredPoint = point;
            InvalidateVisual();
        }

        void ClearHover()
        {
            if (hoveredPoint is null)
                return;
            hoveredPoint = null;
            InvalidateVisual();
        }

        void EndActiveDrag()
        {
            if (dragMoved)
                viewModel?.EndDragFromCanvas();
            dragMoved = false;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            TryRemovePointAt(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton != MouseButton.Middle)
                return;
            if (isDragging)
                return;
            isPanning = true;
            panMoved = false;
            lastPanPosition = e.GetPosition(this);
            Focus();
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton != MouseButton.Middle || !isPanning)
                return;
            isPanning = false;
            ReleaseMouseCapture();
            if (!panMoved)
                TryRemovePointAt(e.GetPosition(this));
            panMoved = false;
            e.Handled = true;
        }

        bool TryRemovePointAt(Point display)
        {
            if (viewModel is null)
                return false;
            var layout = GetLayout();
            if (layout is null)
                return false;

            var hit = HitTestPoint(display, layout.Value);
            if (hit is null)
                return false;
            viewModel.RemovePointFromCanvas(hit);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (viewModel is null)
                return;
            if (e.Key == Key.Delete)
            {
                viewModel.RemoveSelectedPointsFromCanvas();
                e.Handled = true;
            }
        }

        VectorFieldPointItemViewModel? HitTestPoint(Point display, CanvasLayout layout)
        {
            VectorFieldPointItemViewModel? nearest = null;
            var nearestDistSq = PointHitRadius * PointHitRadius;
            for (var i = points.Count - 1; i >= 0; i--)
            {
                var p = LocalToDisplay(GetPointPosition(points[i]), layout);
                var dx = p.X - display.X;
                var dy = p.Y - display.Y;
                var distSq = dx * dx + dy * dy;
                if (distSq <= nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = points[i];
                }
            }
            return nearest;
        }

        static System.Windows.Media.Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        static Pen CreateFrozenPen(Color color, double thickness)
        {
            var pen = new Pen(CreateFrozenBrush(color), thickness);
            pen.Freeze();
            return pen;
        }

        static Pen CreateFrozenDashedPen(Color color, double thickness)
        {
            var pen = new Pen(CreateFrozenBrush(color), thickness) { DashStyle = DashStyles.Dash };
            pen.Freeze();
            return pen;
        }

        static System.Windows.Media.Brush CreateCheckerBrush()
        {
            var dark = CreateFrozenBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            var light = CreateFrozenBrush(Color.FromRgb(0x33, 0x33, 0x33));
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = System.Windows.Media.Stretch.None,
            };
            brush.Freeze();
            return brush;
        }
    }
}
