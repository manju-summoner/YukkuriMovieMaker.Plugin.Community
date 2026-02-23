using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class PortView
{
    public PortView()
    {
        InitializeComponent();
        Loaded += UpdatePosition;
        LayoutUpdated += UpdatePosition;
    }

    private void UpdatePosition(object? sender, EventArgs e)
    {
        if (DataContext is not PortViewModel vm)
            return;

        var graphView = FindAncestor<GraphView>(this);
        if (graphView == null)
            return;

        var transform = TransformToAncestor(graphView);
        var center = transform.Transform(
            new Point(ActualWidth / 2, ActualHeight / 2));

        vm.Position = center;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PortViewModel port) return;

        var graph = FindAncestor<GraphView>(this)?.DataContext as GraphViewModel;
        if (graph == null) return;

        graph.DraggingFromPort = port;
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PortViewModel target) return;

        var graph = FindAncestor<GraphView>(this)?.DataContext as GraphViewModel;
        if (graph == null) return;
        if (graph.DraggingFromPort == null) return;

        graph.ConnectPortsCommand.Execute((graph.DraggingFromPort, target));

        graph.DraggingFromPort = null;
        graph.TemporaryEndPoint = null;

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? obj)
        where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj);
        }

        return null;
    }
}