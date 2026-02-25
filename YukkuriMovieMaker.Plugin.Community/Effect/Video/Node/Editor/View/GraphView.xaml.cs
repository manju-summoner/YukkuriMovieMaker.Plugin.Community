using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class GraphView
{
    private ConnectionAdorner? _connectionAdorner;

    public GraphView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var layer = AdornerLayer.GetAdornerLayer(RootGrid);
        if (layer != null)
        {
            _connectionAdorner = new ConnectionAdorner(RootGrid);
            layer.Add(_connectionAdorner);
            _connectionAdorner.DataContext = DataContext;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is GraphViewModel vm)
        {
            vm.Connections.CollectionChanged += (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(() => { _connectionAdorner?.InvalidateVisual(); }),
                    DispatcherPriority.Loaded);
            };
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;
        if (vm.DraggingFromPort == null) return;

        vm.TemporaryEndPoint = e.GetPosition(RootGrid);
        _connectionAdorner?.InvalidateVisual();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;
        if (vm.DraggingFromPort == null) return;

        vm.DraggingFromPort = null;
        vm.TemporaryEndPoint = null;
        _connectionAdorner?.InvalidateVisual();
    }
}