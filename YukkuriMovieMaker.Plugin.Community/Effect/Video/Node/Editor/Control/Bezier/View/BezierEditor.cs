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

    private static readonly Media.Pen GridPen = CreatePen(Media.Brushes.Gainsboro, 1);
    private static readonly Media.Pen BorderPen = CreatePen(Media.Brushes.Gray, 1);
    private static readonly Media.Pen CurvePen = CreatePen(Media.Brushes.DodgerBlue, 2);
    private static readonly Media.Pen HandlePen = CreatePen(Media.Brushes.Gray, 1);

    private static readonly Media.Brush NodeBrush = Media.Brushes.White;
    private static readonly Media.Brush SelectedNodeBrush = Media.Brushes.DodgerBlue;
    private static readonly Media.Brush HandleBrush = Media.Brushes.DodgerBlue;

    private BezierDragContext? _dragContext;

    static BezierEditor()
    {
        FocusableProperty.OverrideMetadata(
            typeof(BezierEditor),
            new FrameworkPropertyMetadata(true));
    }

    public BezierEditor()
    {
        SnapsToDevicePixels = true;
    }

    public BezierEditorViewModel? ViewModel
    {
        get => (BezierEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public Point ToScreen(Point p)
    {
        var w = Math.Max(1, ActualWidth - MarginSize * 2);
        var h = Math.Max(1, ActualHeight - MarginSize * 2);

        return new Point(
            MarginSize + p.X * w,
            MarginSize + (1.0 - p.Y) * h);
    }

    public Point FromScreen(Point p)
    {
        var w = Math.Max(1, ActualWidth - MarginSize * 2);
        var h = Math.Max(1, ActualHeight - MarginSize * 2);

        return new Point(
            (p.X - MarginSize) / w,
            1.0 - (p.Y - MarginSize) / h);
    }

    public event EventHandler? CurveChanged;
    public event EventHandler? EditCompleted;

    private static Media.Pen CreatePen(Media.Brush brush, double thickness)
    {
        var pen = new Media.Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(Media.DrawingContext dc)
    {
        base.OnRender(dc);

        dc.DrawRectangle(
            Media.Brushes.White,
            new Media.Pen(Media.Brushes.Gray, 1),
            new Rect(0, 0, ActualWidth, ActualHeight));

        DrawGrid(dc);

        if (ViewModel is null)
            return;

        DrawCurve(dc);
        DrawMonotonicWarnings(dc);
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

                        ViewModel.SelectedNode = node;

                        _dragContext = new BezierDragContext(
                            BezierHitType.Node,
                            node,
                            mouse);

                        CaptureMouse();

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
                    _dragContext.Node,
                    model - _dragContext.Node.Position);
                break;

            case BezierHitType.OutHandle:
                BezierEditingUtility.MoveOutHandle(
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

        if (_dragContext is null)
            return;

        ReleaseMouseCapture();

        _dragContext = null;
    }

    private void DrawGrid(Media.DrawingContext dc)
    {
        var width = Math.Max(1, ActualWidth - MarginSize * 2);
        var height = Math.Max(1, ActualHeight - MarginSize * 2);

        for (var i = 0; i <= 4; i++)
        {
            var x = MarginSize + width * i / 4.0;
            var y = MarginSize + height * i / 4.0;

            dc.DrawLine(
                GridPen,
                new Point(x, MarginSize),
                new Point(x, MarginSize + height));

            dc.DrawLine(
                GridPen,
                new Point(MarginSize, y),
                new Point(MarginSize + width, y));
        }

        dc.DrawRectangle(
            null,
            BorderPen,
            new Rect(MarginSize, MarginSize, width, height));
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
        foreach (var node in ViewModel!.Curve.Nodes)
        {
            var selected = node == ViewModel.SelectedNode;

            if (!selected)
                continue;

            var center = ToScreen(node.Position);

            if (node.InHandle.Offset.Length > 1e-6)
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

            if (node.OutHandle.Offset.Length > 1e-6)
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