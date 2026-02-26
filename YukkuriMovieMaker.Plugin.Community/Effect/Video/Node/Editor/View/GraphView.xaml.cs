using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class GraphView
{
    private ConnectionAdorner? _connectionAdorner;
    private bool _isSelecting;
    private RectSelectionAdorner? _selectionAdorner;
    private Point _selectionStart;

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

            _selectionAdorner = new RectSelectionAdorner(RootGrid);
            layer.Add(_selectionAdorner);
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

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        if (vm.DraggingFromPort != null) return;

        _isSelecting = true;
        _selectionStart = e.GetPosition(RootGrid);

        vm.ClearSelection();

        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        if (vm.DraggingFromPort != null)
        {
            vm.TemporaryEndPoint = e.GetPosition(RootGrid);
            _connectionAdorner?.InvalidateVisual();
            return;
        }

        if (_isSelecting)
        {
            var current = e.GetPosition(RootGrid);
            _selectionAdorner?.Update(_selectionStart, current);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        if (vm.DraggingFromPort != null)
        {
            vm.DraggingFromPort = null;
            vm.TemporaryEndPoint = null;
            _connectionAdorner?.InvalidateVisual();
            return;
        }

        if (_isSelecting)
        {
            ReleaseMouseCapture();
            _isSelecting = false;

            var rect = _selectionAdorner?.CurrentRect ?? Rect.Empty;

            foreach (var node in vm.Nodes)
            {
                var nodeRect = new Rect(node.X, node.Y, node.Width, node.Height);

                if (rect.IntersectsWith(nodeRect))
                    vm.AddToSelection(node);
            }

            _selectionAdorner?.Clear();
        }
    }
}