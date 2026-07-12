using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.ViewModel;
using Media = System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.View;

public class BezierEditor : System.Windows.Controls.Control, IBezierCoordinateConverter
{
    private const double MarginSize = 20;
    private const double NodeRadius = 5;
    private const double HandleRadius = 4;
    private const double HitRadius = 8;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(BezierEditorViewModel),
            typeof(BezierEditor),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnViewModelChanged));

    public static readonly DependencyProperty ControlBrushProperty =
        DependencyProperty.Register(
            nameof(ControlBrush),
            typeof(Media.Brush),
            typeof(BezierEditor),
            new FrameworkPropertyMetadata(
                SystemColors.ControlBrush,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnBrushPropertyChanged));

    public static readonly DependencyProperty GridBrushProperty =
        DependencyProperty.Register(
            nameof(GridBrush),
            typeof(Media.Brush),
            typeof(BezierEditor),
            new FrameworkPropertyMetadata(
                SystemColors.ActiveBorderBrush,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnBrushPropertyChanged));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(
            nameof(AccentBrush),
            typeof(Media.Brush),
            typeof(BezierEditor),
            new FrameworkPropertyMetadata(
                SystemColors.AccentColorBrush,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnBrushPropertyChanged));

    private BezierDragContext? _dragContext;

    private Vector _panOffset;

    private Point? _panStartMouse;
    private Vector _panStartOffset;

    static BezierEditor()
    {
        FocusableProperty.OverrideMetadata(
            typeof(BezierEditor),
            new FrameworkPropertyMetadata(true));
    }

    public BezierEditor()
    {
        SnapsToDevicePixels = true;
        UpdatePens();
    }

    public Media.Brush ControlBrush
    {
        get => (Media.Brush)GetValue(ControlBrushProperty);
        set => SetValue(ControlBrushProperty, value);
    }

    public Media.Brush GridBrush
    {
        get => (Media.Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Media.Brush AccentBrush
    {
        get => (Media.Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    private Media.Pen GridPen { get; set; } = null!;

    private Media.Pen BorderPen { get; set; } = null!;

    private Media.Pen CurvePen { get; set; } = null!;

    private Media.Pen HandlePen { get; set; } = null!;

    private Media.Brush NodeBrush => ControlBrush;
    private Media.Brush SelectedNodeBrush => AccentBrush;
    private Media.Brush HandleBrush => AccentBrush;

    public BezierEditorViewModel? ViewModel
    {
        get => (BezierEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public Point ToScreen(Point p)
    {
        var q = ToScreenBase(p);

        return new Point(q.X + _panOffset.X, q.Y + _panOffset.Y);
    }

    public Point FromScreen(Point p)
    {
        var w = Math.Max(1, ActualWidth - MarginSize * 2);
        var h = Math.Max(1, ActualHeight - MarginSize * 2);

        var q = new Point(p.X - _panOffset.X, p.Y - _panOffset.Y);

        return new Point(
            (q.X - MarginSize) / w,
            1.0 - (q.Y - MarginSize) / h);
    }

    private static void OnBrushPropertyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var editor = (BezierEditor)d;

        editor.UpdatePens();
        editor.InvalidateVisual();
    }

    private void UpdatePens()
    {
        GridPen = CreatePen(GridBrush, 1);
        BorderPen = CreatePen(GridBrush, 1);
        CurvePen = CreatePen(AccentBrush, 2);
        HandlePen = CreatePen(GridBrush, 1);
    }

    /// <summary>
    ///     パンオフセットを適用しないベース座標変換。
    ///     パン可能範囲の算出(コンテンツの外接矩形の計算)に用いる。
    /// </summary>
    private Point ToScreenBase(Point p)
    {
        var w = Math.Max(1, ActualWidth - MarginSize * 2);
        var h = Math.Max(1, ActualHeight - MarginSize * 2);

        return new Point(
            MarginSize + p.X * w,
            MarginSize + (1.0 - p.Y) * h);
    }

    public event EventHandler? CurveChanged;
    public event EventHandler? EditCompleted;

    private static Media.Pen CreatePen(Media.Brush brush, double thickness)
    {
        var pen = new Media.Pen(brush, thickness);

        if (pen.CanFreeze)
            pen.Freeze();

        return pen;
    }

    protected override void OnRender(Media.DrawingContext dc)
    {
        base.OnRender(dc);

        dc.DrawRectangle(
            ControlBrush,
            new Media.Pen(GridBrush, 1),
            new Rect(0, 0, ActualWidth, ActualHeight));

        DrawGrid(dc);

        if (ViewModel is null)
            return;

        DrawCurve(dc);
        DrawHandles(dc);
        DrawNodes(dc);
    }

    private static void OnViewModelChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        ((BezierEditor)d).InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (ViewModel is null)
            return;

        Focus();

        var mouse = e.GetPosition(this);

        var hit = BezierHitTester.HitTest(
            ViewModel.Curve,
            this,
            mouse,
            NodeRadius,
            HandleRadius,
            HitRadius);

        switch (e.ChangedButton)
        {
            case MouseButton.Middle:
            {
                _panStartMouse = mouse;
                _panStartOffset = _panOffset;

                CaptureMouse();

                e.Handled = true;
                return;
            }
            case MouseButton.Right:
            {
                if (hit.Node is not null)
                {
                    ViewModel.SelectedNode = hit.Node;

                    ShowContextMenu(hit.Node);

                    InvalidateVisual();
                }

                e.Handled = true;
                return;
            }
            case MouseButton.Left:
                switch (hit.HitType)
                {
                    case BezierHitType.Node:
                    {
                        ViewModel.SelectedNode = hit.Node;

                        _dragContext = new BezierDragContext(
                            hit.HitType,
                            hit.Node!,
                            mouse);

                        CaptureMouse();
                        break;
                    }

                    case BezierHitType.InHandle:
                    case BezierHitType.OutHandle:
                    {
                        ViewModel.SelectedNode = hit.Node;

                        _dragContext = new BezierDragContext(
                            hit.HitType,
                            hit.Node!,
                            mouse);

                        CaptureMouse();
                        break;
                    }

                    case BezierHitType.Segment:
                    {
                        var node = BezierEditingUtility.InsertNode(
                            ViewModel.Curve,
                            hit.SegmentIndex,
                            hit.T);
                        CurveChanged?.Invoke(this, EventArgs.Empty);
                        EditCompleted?.Invoke(this, EventArgs.Empty);

                        ViewModel.SelectedNode = node;

                        InvalidateVisual();
                        break;
                    }

                    default:
                    {
                        ViewModel.SelectedNode = null;
                        InvalidateVisual();
                        break;
                    }
                }

                e.Handled = true;
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (ViewModel is null)
            return;

        var mouse = e.GetPosition(this);

        if (_panStartMouse is { } panStart)
        {
            var delta = mouse - panStart;
            var newOffset = _panStartOffset + delta;

            newOffset = ClampPanOffset(newOffset);

            _panOffset = newOffset;

            InvalidateVisual();
            return;
        }

        if (_dragContext is null)
        {
            var hit = BezierHitTester.HitTest(
                ViewModel.Curve,
                this,
                mouse,
                NodeRadius,
                HandleRadius,
                HitRadius);

            Cursor = hit.HitType switch
            {
                BezierHitType.Node => Cursors.SizeAll,
                BezierHitType.InHandle => Cursors.Hand,
                BezierHitType.OutHandle => Cursors.Hand,
                BezierHitType.Segment => Cursors.Cross,
                _ => Cursors.Arrow
            };

            return;
        }

        var model = FromScreen(mouse);

        switch (_dragContext.HitType)
        {
            case BezierHitType.Node:
                var delta =
                    FromScreen(mouse)
                    - FromScreen(_dragContext.MouseDownPosition);

                BezierEditingUtility.MoveNode(
                    ViewModel.Curve,
                    _dragContext.Node,
                    _dragContext.OriginalNodePosition + delta);
                break;

            case BezierHitType.InHandle:
                BezierEditingUtility.MoveInHandle(
                    ViewModel.Curve,
                    _dragContext.Node,
                    model - _dragContext.Node.Position);
                break;

            case BezierHitType.OutHandle:
                BezierEditingUtility.MoveOutHandle(
                    ViewModel.Curve,
                    _dragContext.Node,
                    model - _dragContext.Node.Position);
                break;
        }

        CurveChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_panStartMouse is not null)
        {
            _panStartMouse = null;

            ReleaseMouseCapture();
            return;
        }

        if (_dragContext is null)
            return;

        _dragContext = null;

        ReleaseMouseCapture();
        EditCompleted?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_panStartMouse is not null)
        {
            _panStartMouse = null;
            ReleaseMouseCapture();
        }

        if (_dragContext is null)
            return;

        ReleaseMouseCapture();

        _dragContext = null;
    }

    /// <summary>
    ///     パンオフセットを許容範囲内にクランプする。
    ///     許容範囲は「曲線・グリッド・制御点ハンドルを含む最小矩形(回転なし)に、
    ///     グリッドとコントロールの間の余白(MarginSize)を加えた大きさ」に基づき、
    ///     コンテンツがコントロール表示領域からはみ出す分だけパンできるようにする。
    ///     コンテンツがコントロール内に収まる場合はパンを許可しない(オフセットは0にクランプされる)。
    /// </summary>
    private Vector ClampPanOffset(Vector offset)
    {
        var contentRect = ComputeContentBounds();

        var viewportWidth = ActualWidth;
        var viewportHeight = ActualHeight;

        double minX, maxX;

        if (contentRect.Width <= viewportWidth)
        {
            minX = 0;
            maxX = 0;
        }
        else
        {
            minX = viewportWidth - contentRect.Right;
            maxX = -contentRect.Left;
        }

        double minY, maxY;

        if (contentRect.Height <= viewportHeight)
        {
            minY = 0;
            maxY = 0;
        }
        else
        {
            minY = viewportHeight - contentRect.Bottom;
            maxY = -contentRect.Top;
        }

        return new Vector(
            Math.Clamp(offset.X, minX, maxX),
            Math.Clamp(offset.Y, minY, maxY));
    }

    /// <summary>
    ///     曲線のノード・制御点ハンドル・グリッドを含む、パンオフセット適用前(ToScreenBase基準)の
    ///     最小外接矩形に、グリッドとコントロールの間の余白(MarginSize)を加えたものを返す。
    /// </summary>
    private Rect ComputeContentBounds()
    {
        var gridWidth = Math.Max(1, ActualWidth - MarginSize * 2);
        var gridHeight = Math.Max(1, ActualHeight - MarginSize * 2);

        var minX = MarginSize;
        var minY = MarginSize;
        var maxX = MarginSize + gridWidth;
        var maxY = MarginSize + gridHeight;

        if (ViewModel is not null)
            foreach (var node in ViewModel.Curve.Nodes)
            {
                var position = ToScreenBase(node.Position);
                var inPoint = ToScreenBase(node.InControlPoint);
                var outPoint = ToScreenBase(node.OutControlPoint);

                foreach (var p in new[] { position, inPoint, outPoint })
                {
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                }
            }

        return new Rect(
            minX - MarginSize,
            minY - MarginSize,
            maxX - minX + MarginSize * 2,
            maxY - minY + MarginSize * 2);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        _panOffset = ClampPanOffset(_panOffset);
    }

    private void DrawGrid(Media.DrawingContext dc)
    {
        var width = Math.Max(1, ActualWidth - MarginSize * 2);
        var height = Math.Max(1, ActualHeight - MarginSize * 2);

        for (var i = 0; i <= 4; i++)
        {
            var x = MarginSize + width * i / 4.0 + _panOffset.X;
            var y = MarginSize + height * i / 4.0 + _panOffset.Y;

            dc.DrawLine(
                GridPen,
                new Point(x, MarginSize + _panOffset.Y),
                new Point(x, MarginSize + height + _panOffset.Y));

            dc.DrawLine(
                GridPen,
                new Point(MarginSize + _panOffset.X, y),
                new Point(MarginSize + width + _panOffset.X, y));
        }

        dc.DrawRectangle(
            null,
            GridPen,
            new Rect(
                MarginSize + _panOffset.X,
                MarginSize + _panOffset.Y,
                width,
                height));
    }

    private void DrawCurve(Media.DrawingContext dc)
    {
        var curve = ViewModel!.Curve;

        var geometry = new Media.StreamGeometry();

        using (var context = geometry.Open())
        {
            var first = true;

            foreach (var segment in curve.GetSegments())
            {
                if (first)
                {
                    context.BeginFigure(ToScreen(segment.P0), false, false);
                    first = false;
                }

                context.BezierTo(
                    ToScreen(segment.P1),
                    ToScreen(segment.P2),
                    ToScreen(segment.P3),
                    true,
                    false);
            }
        }

        geometry.Freeze();

        dc.DrawGeometry(
            null,
            CurvePen,
            geometry);
    }

    private void DrawHandles(Media.DrawingContext dc)
    {
        var nodes = ViewModel!.Curve.Nodes;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var selected = node == ViewModel.SelectedNode;

            if (!selected)
                continue;

            var center = ToScreen(node.Position);

            var isFirst = i == 0;
            var isLast = i == nodes.Count - 1;

            var showIn = !isFirst && (isLast || node.InHandle.Offset.Length > 1e-6);
            var showOut = !isLast && (isFirst || node.OutHandle.Offset.Length > 1e-6);

            if (showIn)
            {
                var input = ToScreen(node.InControlPoint);

                dc.DrawLine(HandlePen, center, input);

                dc.DrawEllipse(
                    HandleBrush,
                    BorderPen,
                    input,
                    HandleRadius,
                    HandleRadius);
            }

            if (showOut)
            {
                var output = ToScreen(node.OutControlPoint);

                dc.DrawLine(HandlePen, center, output);

                dc.DrawEllipse(
                    HandleBrush,
                    BorderPen,
                    output,
                    HandleRadius,
                    HandleRadius);
            }
        }
    }

    private void DrawNodes(Media.DrawingContext dc)
    {
        foreach (var node in ViewModel!.Curve.Nodes)
        {
            var selected = node == ViewModel.SelectedNode;

            dc.DrawEllipse(
                selected ? SelectedNodeBrush : NodeBrush,
                selected ? CurvePen : BorderPen,
                ToScreen(node.Position),
                NodeRadius,
                NodeRadius);
        }
    }

    private void DrawMonotonicWarnings(Media.DrawingContext dc)
    {
        var curve = ViewModel!.Curve;

        foreach (var segment in curve.GetSegments())
        {
            var regions = BezierMonotonicAnalyzer.FindNonMonotonicXRegions(segment);

            foreach (var (t0, t1) in regions)
            {
                var p0 = ToScreen(BezierUtility.Evaluate(segment, t0));
                var p1 = ToScreen(BezierUtility.Evaluate(segment, t1));

                var pen = new Media.Pen(Media.Brushes.Red, 1)
                {
                    DashStyle = new Media.DashStyle([4.0, 3.0], 0)
                };

                dc.DrawLine(pen, p0, p1);
            }
        }
    }

    private void ShowContextMenu(BezierNode node)
    {
        var menu = new ContextMenu();

        var smooth = new MenuItem
        {
            Header = "Smooth",
            IsCheckable = true,
            IsChecked = node.Type == BezierNodeType.Smooth
        };

        smooth.Click += (_, _) =>
        {
            BezierEditingUtility.SetNodeType(
                ViewModel!.Curve,
                node,
                BezierNodeType.Smooth);

            CurveChanged?.Invoke(this, EventArgs.Empty);
            EditCompleted?.Invoke(this, EventArgs.Empty);

            InvalidateVisual();
        };

        menu.Items.Add(smooth);

        var corner = new MenuItem
        {
            Header = "Corner",
            IsCheckable = true,
            IsChecked = node.Type == BezierNodeType.Corner
        };

        corner.Click += (_, _) =>
        {
            BezierEditingUtility.SetNodeType(
                ViewModel!.Curve,
                node,
                BezierNodeType.Corner);

            CurveChanged?.Invoke(this, EventArgs.Empty);
            EditCompleted?.Invoke(this, EventArgs.Empty);

            InvalidateVisual();
        };

        menu.Items.Add(corner);

        menu.Items.Add(new Separator());

        var delete = new MenuItem
        {
            Header = "Delete",
            IsEnabled = !node.IsFixed
        };

        delete.Click += (_, _) =>
        {
            BezierEditingUtility.DeleteNode(
                ViewModel!.Curve,
                node);

            ViewModel.SelectedNode = null;

            CurveChanged?.Invoke(this, EventArgs.Empty);
            EditCompleted?.Invoke(this, EventArgs.Empty);

            InvalidateVisual();
        };

        menu.Items.Add(delete);

        menu.IsOpen = true;
    }
}