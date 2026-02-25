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
    private double _startX;
    private double _startY;

    public NodeView()
    {
        InitializeComponent();

        Loaded += (_, _) => _rootCanvas = FindParent<Canvas>(this);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not NodeViewModel vm) return;

        _isMouseDown = true;
        _isDragging = false;

        _mouseDownPos = e.GetPosition(_rootCanvas);

        _startX = vm.X;
        _startY = vm.Y;

        e.Handled = true;
        Focus();
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown) return;
        if (DataContext is not NodeViewModel vm) return;

        var currentPos = e.GetPosition(_rootCanvas);
        var delta = currentPos - _mouseDownPos;

        if (!_isDragging)
        {
            if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
                _isDragging = true;
            else return;
        }

        vm.X = _startX + delta.X;
        vm.Y = _startY + delta.Y;

        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown) return;

        ReleaseMouseCapture();

        if (_isDragging)
        {
            if (DataContext is NodeViewModel vm)
                vm.CommitPosition();
        }
        else
        {
            if (DataContext is NodeViewModel vm && FindParent<GraphView>(this)?.DataContext is GraphViewModel graphVm)
                graphVm.SelectSingle(vm);
        }

        _isMouseDown = false;
        _isDragging = false;

        e.Handled = true;
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