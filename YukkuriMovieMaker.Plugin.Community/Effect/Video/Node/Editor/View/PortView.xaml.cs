using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

        Dispatcher.BeginInvoke(() =>
        {
            var graphView = FindAncestor<GraphView>(this);
            if (graphView == null)
                return;

            var root = graphView.FindName("RootGrid") as UIElement;
            if (root == null)
                return;

            if (ActualWidth == 0 || ActualHeight == 0)
                return;

            var transform = TransformToAncestor(root);
            var center = transform.Transform(
                new Point(ActualWidth / 2, ActualHeight / 2));

            vm.Position = center;
        }, DispatcherPriority.Render);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var graph = FindAncestor<GraphView>(this)?.DataContext as GraphViewModel;
        if (graph == null) return;
        if (e.ClickCount == 2)
        {
            graph.DraggingFromPort = null;
            graph.TemporaryEndPoint = null;
            if (DataContext is not PortViewModel vm) return;

            var related = graph.Connections
                .Where(c =>
                    (c.FromNodeId == vm.NodeId && c.FromPortName == vm.Name) ||
                    (c.ToNodeId == vm.NodeId && c.ToPortName == vm.Name))
                .ToList();

            foreach (var c in related)
                graph.DisconnectCommand.Execute(c);

            e.Handled = true;
            return;
        }

        if (DataContext is not PortViewModel port) return;

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