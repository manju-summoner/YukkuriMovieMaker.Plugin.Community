using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class NodeView
{
    // ノードのドラッグ用
    private bool _isDragging;
    private Canvas? _rootCanvas;
    private Point _startMousePos;
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

        _isDragging = true;

        _startX = vm.X;
        _startY = vm.Y;

        _startMousePos = e.GetPosition(_rootCanvas);

        e.Handled = true;
        Focus();
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        if (DataContext is not NodeViewModel vm) return;

        var currentPos = e.GetPosition(_rootCanvas);

        var delta = currentPos - _startMousePos;

        vm.X = _startX + delta.X;
        vm.Y = _startY + delta.Y;
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;

        if (DataContext is NodeViewModel vm)
            vm.CommitPosition();
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