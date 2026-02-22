using System.Windows;
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