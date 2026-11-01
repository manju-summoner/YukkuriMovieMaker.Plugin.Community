using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenEditorCanvas : FrameworkElement
    {
        const double MinZoom = 0.05;
        const double MaxZoom = 32.0;
        const double ZoomStep = 1.2;
        const double CheckerCellSize = 8.0;
        internal const double DragThreshold = 3.0;
        const double MaxStabilizationStrength = 0.95;

        static readonly System.Windows.Media.Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Pen BorderPen = CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen BrushSizeShadowPen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 3.0);
        static readonly System.Windows.Media.Pen BrushSizePen = CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);

        internal static readonly System.Windows.Media.Pen OutlineShadowPen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1.0);
        internal static readonly System.Windows.Media.Pen OutlinePen = CreateFrozenDashedPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);

        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DocumentImageProperty =
            DependencyProperty.Register(nameof(DocumentImage), typeof(ImageSource), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CanvasWidthProperty =
            DependencyProperty.Register(nameof(CanvasWidth), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1920d, FrameworkPropertyMetadataOptions.AffectsRender, OnCanvasSizeChanged));

        public static readonly DependencyProperty CanvasHeightProperty =
            DependencyProperty.Register(nameof(CanvasHeight), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1080d, FrameworkPropertyMetadataOptions.AffectsRender, OnCanvasSizeChanged));

        public static readonly DependencyProperty WetInkColorProperty =
            DependencyProperty.Register(nameof(WetInkColor), typeof(Color), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(Colors.White, OnWetInkStyleChanged));

        public static readonly DependencyProperty IsWetInkOverlayProperty =
            DependencyProperty.Register(nameof(IsWetInkOverlay), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty WetInkThicknessProperty =
            DependencyProperty.Register(nameof(WetInkThickness), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(10d, OnBrushSizeChanged));

        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(true, OnIsEditableChanged));

        public static readonly DependencyProperty IsRectangleSelectionProperty =
            DependencyProperty.Register(nameof(IsRectangleSelection), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsFillModeProperty =
            DependencyProperty.Register(nameof(IsFillMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, OnBrushSizeChanged));

        public static readonly DependencyProperty IsSelectionModeProperty =
            DependencyProperty.Register(nameof(IsSelectionMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnBrushSizeChanged));

        public static readonly DependencyProperty IsOrderModeProperty =
            DependencyProperty.Register(nameof(IsOrderMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsOrderModeChanged));

        public static readonly DependencyProperty OrderBadgesProperty =
            DependencyProperty.Register(nameof(OrderBadges), typeof(PenOrderBadge[]), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, OnOrderBadgesChanged));

        public static readonly DependencyProperty SelectionBoundsProperty =
            DependencyProperty.Register(nameof(SelectionBounds), typeof(Rect), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IgnoresPressureProperty =
            DependencyProperty.Register(nameof(IgnoresPressure), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty TaperLengthProperty =
            DependencyProperty.Register(nameof(TaperLength), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(0d, null, CoerceTaperLength));

        public static readonly DependencyProperty StabilizationStrengthProperty =
            DependencyProperty.Register(nameof(StabilizationStrength), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(0d, null, CoerceStabilizationStrength));

        public static readonly DependencyProperty WetInkUsesPressureProperty =
            DependencyProperty.Register(nameof(WetInkUsesPressure), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceZoom));

        public ImageSource? Image
        {
            get => (ImageSource?)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        public ImageSource? DocumentImage
        {
            get => (ImageSource?)GetValue(DocumentImageProperty);
            set => SetValue(DocumentImageProperty, value);
        }

        public double CanvasWidth
        {
            get => (double)GetValue(CanvasWidthProperty);
            set => SetValue(CanvasWidthProperty, value);
        }

        public double CanvasHeight
        {
            get => (double)GetValue(CanvasHeightProperty);
            set => SetValue(CanvasHeightProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public Color WetInkColor
        {
            get => (Color)GetValue(WetInkColorProperty);
            set => SetValue(WetInkColorProperty, value);
        }

        public bool IsWetInkOverlay
        {
            get => (bool)GetValue(IsWetInkOverlayProperty);
            set => SetValue(IsWetInkOverlayProperty, value);
        }

        public double WetInkThickness
        {
            get => (double)GetValue(WetInkThicknessProperty);
            set => SetValue(WetInkThicknessProperty, value);
        }

        public bool WetInkUsesPressure
        {
            get => (bool)GetValue(WetInkUsesPressureProperty);
            set => SetValue(WetInkUsesPressureProperty, value);
        }

        public bool IgnoresPressure
        {
            get => (bool)GetValue(IgnoresPressureProperty);
            set => SetValue(IgnoresPressureProperty, value);
        }

        public double TaperLength
        {
            get => (double)GetValue(TaperLengthProperty);
            set => SetValue(TaperLengthProperty, value);
        }

        public double StabilizationStrength
        {
            get => (double)GetValue(StabilizationStrengthProperty);
            set => SetValue(StabilizationStrengthProperty, value);
        }

        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public bool IsRectangleSelection
        {
            get => (bool)GetValue(IsRectangleSelectionProperty);
            set => SetValue(IsRectangleSelectionProperty, value);
        }

        public bool IsFillMode
        {
            get => (bool)GetValue(IsFillModeProperty);
            set => SetValue(IsFillModeProperty, value);
        }

        public bool IsSelectionMode
        {
            get => (bool)GetValue(IsSelectionModeProperty);
            set => SetValue(IsSelectionModeProperty, value);
        }

        public bool IsOrderMode
        {
            get => (bool)GetValue(IsOrderModeProperty);
            set => SetValue(IsOrderModeProperty, value);
        }

        public PenOrderBadge[]? OrderBadges
        {
            get => (PenOrderBadge[]?)GetValue(OrderBadgesProperty);
            set => SetValue(OrderBadgesProperty, value);
        }

        public Rect SelectionBounds
        {
            get => (Rect)GetValue(SelectionBoundsProperty);
            set => SetValue(SelectionBoundsProperty, value);
        }

        public event EventHandler<PenLassoCompletedEventArgs>? LassoCompleted;

        public event EventHandler? SelectionTransformStarted;

        public event EventHandler<PenSelectionTransformedEventArgs>? SelectionTransformed;

        public event EventHandler? SelectionTransformCompleted;

        public event EventHandler? WetStrokeChanged;

        public event EventHandler<PenStrokeCompletedEventArgs>? StrokeCompleted;

        public event EventHandler<PenFillRequestedEventArgs>? FillRequested;

        public event EventHandler<PenViewChangedEventArgs>? ViewChanged;

        public event EventHandler<PenOrderBadgeEventArgs>? OrderBadgePressed;

        public event EventHandler<PenOrderBadgeEventArgs>? OrderBadgeDropped;

        public event EventHandler? OrderBackgroundPressed;

        public event EventHandler? PenInvertedChanged;

        readonly PenStrokeTool strokeTool;
        readonly PenSelectionTool selectionTool;
        readonly PenFillTool fillTool;
        readonly PenOrderTool orderTool;
        readonly PenPointerState pointerState = new();
        readonly DrawingVisual brushSizeVisual = new();
        readonly EllipseGeometry brushSizeGeometry = new();
        readonly TranslateTransform brushSizeTransform = new();

        IPenCanvasTool? activeTool;
        PenInputSource inputSource;
        bool isStrokeInverted;

        Point brushSizePoint;
        bool isPointerInside;

        Point origin;
        bool isPanning;
        bool isPanMoved;
        MouseButton panButton;
        Point panStart;
        bool isSpaceHeld;

        public PenEditorCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
            InputMethod.SetIsInputMethodEnabled(this, false);

            Cursor = Cursors.Cross;
            Loaded += OnCanvasLoaded;
            Unloaded += OnCanvasUnloaded;
            pointerState.InvertedChanged += OnPointerInvertedChanged;

            strokeTool = new PenStrokeTool(this);
            selectionTool = new PenSelectionTool(this);
            fillTool = new PenFillTool(this);
            orderTool = new PenOrderTool(this);
            AddVisualChild(strokeTool.Visual);
            for (var i = 0; i < orderTool.VisualCount; i++)
                AddVisualChild(orderTool.GetVisual(i));

            brushSizeVisual.Transform = brushSizeTransform;
            brushSizeVisual.Opacity = 0;
            using (var context = brushSizeVisual.RenderOpen())
            {
                context.DrawGeometry(null, BrushSizeShadowPen, brushSizeGeometry);
                context.DrawGeometry(null, BrushSizePen, brushSizeGeometry);
            }
            AddVisualChild(brushSizeVisual);
            strokeTool.SetStyle(WetInkColor);
        }

        void OnCanvasLoaded(object sender, RoutedEventArgs e)
        {
            pointerState.Attach(this);
            orderTool.SetPixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip);
            Focus();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            orderTool.SetPixelsPerDip(newDpi.PixelsPerDip);
        }

        void OnCanvasUnloaded(object sender, RoutedEventArgs e) => pointerState.Dispose();

        protected override int VisualChildrenCount => orderTool.VisualCount + 2;

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
                return strokeTool.Visual;
            var orderIndex = index - 1;
            if (orderIndex < orderTool.VisualCount)
                return orderTool.GetVisual(orderIndex);
            return orderIndex == orderTool.VisualCount
                ? brushSizeVisual
                : throw new ArgumentOutOfRangeException(nameof(index));
        }

        internal void AttachVisual(Visual visual) => AddVisualChild(visual);

        internal void DetachVisual(Visual visual) => RemoveVisualChild(visual);

        internal void RaiseWetStrokeChanged() => WetStrokeChanged?.Invoke(this, EventArgs.Empty);

        internal void RaiseStrokeCompleted(StylusPointCollection points)
            => StrokeCompleted?.Invoke(this, new PenStrokeCompletedEventArgs(points, isStrokeInverted));

        internal void RaiseFillRequested(PenFillRequestedEventArgs e) => FillRequested?.Invoke(this, e);

        internal void RaiseLassoCompleted(List<Point> points)
            => LassoCompleted?.Invoke(this, new PenLassoCompletedEventArgs(points));

        internal void RaiseSelectionTransformStarted() => SelectionTransformStarted?.Invoke(this, EventArgs.Empty);

        internal void RaiseSelectionTransformed(Matrix matrix)
            => SelectionTransformed?.Invoke(this, new PenSelectionTransformedEventArgs(matrix));

        internal void RaiseSelectionTransformCompleted() => SelectionTransformCompleted?.Invoke(this, EventArgs.Empty);

        internal void RaiseOrderBadgePressed(PenOrderBadgeEventArgs e) => OrderBadgePressed?.Invoke(this, e);

        internal void RaiseOrderBadgeDropped(PenOrderBadgeEventArgs e) => OrderBadgeDropped?.Invoke(this, e);

        internal void RaiseOrderBackgroundPressed() => OrderBackgroundPressed?.Invoke(this, EventArgs.Empty);

        public bool IsStrokeInProgress => activeTool is not null;

        public bool IsPenInverted => pointerState.IsInverted;

        public StylusPointCollection? WetStrokePoints => strokeTool.WetPoints;

        IPenCanvasTool ModeTool
            => IsPenInverted ? strokeTool : IsOrderMode ? orderTool : IsFillMode ? fillTool : IsSelectionMode ? selectionTool : strokeTool;

        void OnPointerInvertedChanged(object? sender, EventArgs e)
        {
            PenInvertedChanged?.Invoke(this, EventArgs.Empty);
            UpdateBrushSize();
            if (!IsStrokeInProgress)
                UpdateCursor(brushSizePoint);
        }

        public void BeginStroke(Point canvasPoint, float pressure)
        {
            if (IsStrokeInProgress)
                return;

            var tool = ModeTool;
            if (!ReferenceEquals(tool, orderTool) && !IsEditable)
                return;

            isStrokeInverted = IsPenInverted;
            if (tool.Begin(canvasPoint, pressure))
                activeTool = tool;
        }

        public void AddStrokePoint(Point canvasPoint, float pressure)
            => activeTool?.Move(canvasPoint, pressure);

        public void EndStroke()
        {
            inputSource = PenInputSource.None;

            var tool = activeTool;
            activeTool = null;
            tool?.End();
        }

        public void ResetView()
        {
            var size = RenderSize;
            if (size.Width <= 0 || size.Height <= 0 || CanvasWidth <= 0 || CanvasHeight <= 0)
                return;

            Zoom = Math.Min(size.Width / CanvasWidth, size.Height / CanvasHeight);
            origin = new Point(
                (size.Width - CanvasWidth * Zoom) / 2,
                (size.Height - CanvasHeight * Zoom) / 2);
            UpdateViewTransforms();
            UpdateBrushSize();
            RaiseViewChanged();
            InvalidateVisual();
        }

        void RaiseViewChanged()
            => ViewChanged?.Invoke(this, new PenViewChangedEventArgs(Zoom, origin, RenderSize, VisualTreeHelper.GetDpi(this).DpiScaleX));

        public Point ScreenToCanvas(Point point)
        {
            var zoom = Zoom;
            return new Point((point.X - origin.X) / zoom, (point.Y - origin.Y) / zoom);
        }

        public Point CanvasToScreen(Point point)
        {
            var zoom = Zoom;
            return new Point(point.X * zoom + origin.X, point.Y * zoom + origin.Y);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            ResetView();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var size = RenderSize;
            drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(size));

            var rect = GetCanvasRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            drawingContext.DrawRectangle(CheckerBrush, null, rect);
            var image = Image;
            if (image is not null)
                drawingContext.DrawImage(image, rect);
            var documentImage = DocumentImage;
            if (documentImage is not null)
                drawingContext.DrawImage(documentImage, new Rect(0, 0, documentImage.Width, documentImage.Height));
            drawingContext.DrawRectangle(null, BorderPen, rect);
            selectionTool.Render(drawingContext);
            orderTool.Render(drawingContext);
        }

        protected override void OnStylusDown(StylusDownEventArgs e)
        {
            base.OnStylusDown(e);
            if (IsStrokeInProgress || IsSpaceKeyDown)
                return;

            Focus();
            var points = e.GetStylusPoints(this);
            if (points.Count > 0)
                BeginStroke(ScreenToCanvas(new Point(points[0].X, points[0].Y)), points[0].PressureFactor);
            if (!IsStrokeInProgress)
            {
                e.Handled = IsFillMode;
                return;
            }
            if (!CaptureStylus())
            {
                EndStroke();
                return;
            }

            inputSource = PenInputSource.Stylus;
            AddStylusPoints(points, 1);
            e.Handled = true;
        }

        protected override void OnStylusMove(StylusEventArgs e)
        {
            base.OnStylusMove(e);
            if (inputSource is not PenInputSource.Stylus || !IsStrokeInProgress)
                return;
            AddStylusPoints(e.GetStylusPoints(this), 0);
            e.Handled = true;
        }

        protected override void OnStylusUp(StylusEventArgs e)
        {
            base.OnStylusUp(e);
            if (inputSource is not PenInputSource.Stylus)
                return;

            if (IsStrokeInProgress)
            {
                AddStylusPoints(e.GetStylusPoints(this), 0);
                EndStroke();
            }
            ReleaseStylusCapture();
            e.Handled = true;
        }

        protected override void OnLostStylusCapture(StylusEventArgs e)
        {
            base.OnLostStylusCapture(e);
            EndStroke();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (isPanning)
            {
                isPanning = false;
                UpdateCursor(brushSizePoint);
            }
            EndStroke();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key is not Key.Space)
                return;

            SetSpaceHeld(true);
            e.Handled = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key is not Key.Space)
                return;

            SetSpaceHeld(false);
            e.Handled = true;
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            SetSpaceHeld(false);
        }

        static bool IsSpaceKeyDown => Keyboard.IsKeyDown(Key.Space);

        void SetSpaceHeld(bool value)
        {
            if (isSpaceHeld == value)
                return;

            isSpaceHeld = value;
            if (!IsStrokeInProgress && !isPanning)
                UpdateCursor(brushSizePoint);
        }

        void BeginPan(MouseButtonEventArgs e)
        {
            isPanning = true;
            isPanMoved = false;
            panButton = e.ChangedButton;
            panStart = e.GetPosition(this);
            Cursor = Cursors.SizeAll;
            CaptureMouse();
            e.Handled = true;
        }

        void EndPan(MouseButtonEventArgs e)
        {
            isPanning = false;
            ReleaseMouseCapture();
            UpdateCursor(ScreenToCanvas(e.GetPosition(this)));
            e.Handled = true;
        }

        void AddStylusPoints(StylusPointCollection points, int startIndex)
        {
            for (var i = startIndex; i < points.Count; i++)
            {
                var point = points[i];
                AddStrokePoint(ScreenToCanvas(new Point(point.X, point.Y)), point.PressureFactor);
            }

            if (points.Count == 0)
                return;

            var last = points[points.Count - 1];
            brushSizePoint = ScreenToCanvas(new Point(last.X, last.Y));
            isPointerInside = true;
            UpdateBrushSize();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta == 0)
                return;

            var position = e.GetPosition(this);
            var canvasPosition = ScreenToCanvas(position);
            Zoom = e.Delta > 0 ? Zoom * ZoomStep : Zoom / ZoomStep;
            origin = new Point(
                position.X - canvasPosition.X * Zoom,
                position.Y - canvasPosition.Y * Zoom);
            UpdateViewTransforms();
            UpdateBrushSize();
            RaiseViewChanged();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (IsStrokeInProgress || isPanning)
                return;

            if (e.ChangedButton is MouseButton.Middle)
            {
                BeginPan(e);
                return;
            }

            if (e.ChangedButton is not MouseButton.Left)
                return;

            Focus();
            if (IsSpaceKeyDown)
            {
                SetSpaceHeld(true);
                BeginPan(e);
                return;
            }

            pointerState.Refresh();
            BeginStroke(ScreenToCanvas(e.GetPosition(this)), GetInputPressure(e));
            if (!IsStrokeInProgress)
            {
                e.Handled = IsFillMode;
                return;
            }
            if (!CaptureMouse())
            {
                EndStroke();
                return;
            }

            inputSource = PenInputSource.Mouse;
            e.Handled = true;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            brushSizePoint = ScreenToCanvas(e.GetPosition(this));
            isPointerInside = true;
            UpdateBrushSize();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            isPointerInside = false;
            UpdateBrushSize();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            pointerState.Refresh();
            brushSizePoint = ScreenToCanvas(e.GetPosition(this));
            isPointerInside = true;
            UpdateBrushSize();
            if (isPanning)
            {
                var panPosition = e.GetPosition(this);
                var delta = panPosition - panStart;
                if (!isPanMoved && Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                    return;

                isPanMoved = true;
                origin = new Point(origin.X + delta.X, origin.Y + delta.Y);
                panStart = panPosition;
                UpdateViewTransforms();
                RaiseViewChanged();
                InvalidateVisual();
                return;
            }

            var canvasPoint = ScreenToCanvas(e.GetPosition(this));
            if (inputSource is not PenInputSource.Mouse || !IsStrokeInProgress)
            {
                if (!IsStrokeInProgress)
                    isSpaceHeld = IsSpaceKeyDown;
                UpdateCursor(canvasPoint);
                return;
            }

            AddStrokePoint(canvasPoint, GetInputPressure(e));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (isPanning && e.ChangedButton == panButton)
            {
                EndPan(e);
                return;
            }

            if (e.ChangedButton is not MouseButton.Left || inputSource is not PenInputSource.Mouse || !IsStrokeInProgress)
                return;

            pointerState.Refresh();
            AddStrokePoint(ScreenToCanvas(e.GetPosition(this)), GetInputPressure(e));
            EndStroke();
            ReleaseMouseCapture();
            e.Handled = true;
        }

        float GetInputPressure(MouseEventArgs e)
        {
            if (e.StylusDevice is { } device)
            {
                var points = device.GetStylusPoints(this);
                if (points.Count > 0)
                    return points[^1].PressureFactor;
            }

            return pointerState.HasPressure ? pointerState.Pressure : SerializableStylusPoint.NeutralPressure;
        }

        void UpdateViewTransforms()
        {
            strokeTool.UpdateTransform(Zoom, origin);
            orderTool.UpdatePositions();
        }

        void UpdateBrushSize()
        {
            var radius = WetInkThickness * Zoom / 2;
            var isBrushHidden = !IsPenInverted && (IsSelectionMode || IsFillMode || IsOrderMode);
            if (!isPointerInside || !IsEditable || isBrushHidden || radius <= 0)
            {
                brushSizeVisual.Opacity = 0;
                return;
            }

            if (brushSizeGeometry.RadiusX != radius)
            {
                brushSizeGeometry.RadiusX = radius;
                brushSizeGeometry.RadiusY = radius;
            }

            var point = CanvasToScreen(brushSizePoint);
            brushSizeTransform.X = point.X;
            brushSizeTransform.Y = point.Y;
            brushSizeVisual.Opacity = 1;
        }

        static void OnBrushSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.UpdateBrushSize();
        }

        void UpdateCursor(Point canvasPoint)
        {
            if (isSpaceHeld)
            {
                Cursor = Cursors.SizeAll;
                return;
            }

            if (IsOrderMode && !IsPenInverted)
            {
                Cursor = orderTool.GetCursor(canvasPoint);
                return;
            }

            if (!IsEditable)
            {
                Cursor = Cursors.No;
                return;
            }

            Cursor = ModeTool.GetCursor(canvasPoint);
        }

        static void OnIsEditableChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not PenEditorCanvas canvas)
                return;

            canvas.Cursor = canvas.IsOrderMode ? Cursors.Arrow : (bool)e.NewValue ? Cursors.Cross : Cursors.No;
            canvas.UpdateBrushSize();
        }

        static void OnIsOrderModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not PenEditorCanvas canvas)
                return;

            canvas.Cursor = (bool)e.NewValue ? Cursors.Arrow : canvas.IsEditable ? Cursors.Cross : Cursors.No;
            canvas.UpdateBrushSize();
            canvas.orderTool.Sync();
        }

        static void OnOrderBadgesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.orderTool.Sync();
        }

        static void OnWetInkStyleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.strokeTool.SetStyle(canvas.WetInkColor);
        }

        Rect GetCanvasRect()
        {
            var zoom = Zoom;
            return new Rect(origin.X, origin.Y, CanvasWidth * zoom, CanvasHeight * zoom);
        }

        static void OnCanvasSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.ResetView();
        }

        static object CoerceTaperLength(DependencyObject sender, object value)
        {
            var length = (double)value;
            return double.IsNaN(length) || length < 0 ? 0d : length;
        }

        static object CoerceStabilizationStrength(DependencyObject sender, object value)
        {
            var strength = (double)value;
            if (double.IsNaN(strength) || strength < 0)
                return 0d;
            return strength > MaxStabilizationStrength ? MaxStabilizationStrength : strength;
        }

        static object CoerceZoom(DependencyObject sender, object value)
        {
            var zoom = (double)value;
            if (double.IsNaN(zoom) || zoom < MinZoom)
                return MinZoom;
            return zoom > MaxZoom ? MaxZoom : zoom;
        }

        internal static System.Windows.Media.Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        internal static System.Windows.Media.Pen CreateFrozenPen(Color color, double thickness)
        {
            var pen = new System.Windows.Media.Pen(CreateFrozenBrush(color), thickness);
            pen.Freeze();
            return pen;
        }

        internal static System.Windows.Media.Pen CreateFrozenDashedPen(Color color, double thickness)
        {
            var pen = new System.Windows.Media.Pen(CreateFrozenBrush(color), thickness)
            {
                DashStyle = new DashStyle([4, 4], 0),
            };
            pen.Freeze();
            return pen;
        }

        static System.Windows.Media.Brush CreateCheckerBrush()
        {
            var light = CreateFrozenBrush(Color.FromRgb(0x50, 0x50, 0x50));
            var dark = CreateFrozenBrush(Color.FromRgb(0x40, 0x40, 0x40));
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                context.DrawRectangle(dark, null, new Rect(0, 0, CheckerCellSize * 2, CheckerCellSize * 2));
                context.DrawRectangle(light, null, new Rect(0, 0, CheckerCellSize, CheckerCellSize));
                context.DrawRectangle(light, null, new Rect(CheckerCellSize, CheckerCellSize, CheckerCellSize, CheckerCellSize));
            }
            drawing.Freeze();
            var brush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, CheckerCellSize * 2, CheckerCellSize * 2),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            };
            brush.Freeze();
            return brush;
        }
    }
}
