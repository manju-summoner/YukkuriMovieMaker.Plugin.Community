using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class NodeView
{
    // ノードのドラッグ・クリック用
    private const double DragThreshold = 4.0;
    private bool _isDragging;

    private bool _isMouseDown;
    private Point _mouseDownPos;
    private Canvas? _rootCanvas;

    public NodeView()
    {
        InitializeComponent();

        Loaded += (_, _) => _rootCanvas = FindParent<Canvas>(this);
        SizeChanged += OnSizeChanged;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = true;
        _isDragging = false;

        if (FindParent<GraphView>(this) is { DataContext: GraphViewModel graphVm } graphView)
            _mouseDownPos = graphVm.TransformToCanvas(e.GetPosition(graphView));

        e.Handled = true;
        Focus();
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown) return;
        if (DataContext is not NodeViewModel vm) return;
        if (FindParent<GraphView>(this) is not { DataContext: GraphViewModel graphVm } graphView) return;

        var currentPos = graphVm.TransformToCanvas(e.GetPosition(graphView));
        var delta = currentPos - _mouseDownPos;

        if (!_isDragging)
        {
            if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
            {
                _isDragging = true;

                graphVm.BeginNodeDrag(vm);
            }
            else return;
        }

        graphVm.UpdateNodeDrag(delta);

        _mouseDownPos = currentPos;

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown) return;

        ReleaseMouseCapture();

        if (_isDragging)
        {
            if (FindParent<GraphView>(this)?.DataContext is GraphViewModel graphVm)
                graphVm.EndNodeDrag();
        }
        else
        {
            if (DataContext is NodeViewModel vm &&
                FindParent<GraphView>(this)?.DataContext is GraphViewModel graphVm)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    if (graphVm.SelectedNodes.Contains(vm))
                    {
                        graphVm.SelectedNodes.Remove(vm);
                        vm.IsSelected = false;
                    }
                    else
                    {
                        graphVm.AddToSelection(vm);
                    }
                }
                else
                {
                    graphVm.SelectSingle(vm);
                }
            }
        }

        _isMouseDown = false;
        _isDragging = false;
        e.Handled = true;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is NodeViewModel vm)
        {
            vm.Width = ActualWidth;
            vm.Height = ActualHeight;
        }
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null)
            return null;
        var parentObject = VisualTreeHelper.GetParent(child);

        return parentObject switch
        {
            null => null,
            T parent => parent,
            _ => FindParent<T>(parentObject)
        };
    }
}