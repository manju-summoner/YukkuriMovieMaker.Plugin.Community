using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View.Adorners;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class GraphView
{
    private ConnectionAdorner? _connectionAdorner;
    private bool _isSelecting;
    private SelectionAdornerBase? _selectionAdorner;
    private Point _selectionStart;

    public GraphView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    public GraphControlMode Mode
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            var layer = AdornerLayer.GetAdornerLayer(RootGrid);
            if (layer is null) return;
            if (_selectionAdorner != null)
                layer.Remove(_selectionAdorner);

            switch (value)
            {
                case GraphControlMode.RectSelection:
                    _selectionAdorner = new RectSelectionAdorner(RootGrid);
                    layer.Add(_selectionAdorner);
                    break;
                case GraphControlMode.LassoSelection:
                    _selectionAdorner = new LassoSelectionAdorner(RootGrid);
                    layer.Add(_selectionAdorner);
                    break;
            }
        }
    } = GraphControlMode.LassoSelection;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var layer = AdornerLayer.GetAdornerLayer(RootGrid);
        if (layer != null && Mode != GraphControlMode.Pan)
        {
            _connectionAdorner = new ConnectionAdorner(RootGrid);
            layer.Add(_connectionAdorner);
            _connectionAdorner.DataContext = DataContext;

            switch (Mode)
            {
                case GraphControlMode.RectSelection:
                    _selectionAdorner = new RectSelectionAdorner(RootGrid);
                    break;
                case GraphControlMode.LassoSelection:
                    _selectionAdorner = new LassoSelectionAdorner(RootGrid);
                    break;
            }

            if (_selectionAdorner != null)
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
        if (Mode is GraphControlMode.RectSelection && _selectionAdorner is RectSelectionAdorner rectSelectionAdorner)
            rectSelectionAdorner.Update(_selectionStart, _selectionStart);
        else if (Mode is GraphControlMode.LassoSelection &&
                 _selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner)
            lassoSelectionAdorner.Begin(_selectionStart);

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
            if (Mode is GraphControlMode.RectSelection &&
                _selectionAdorner is RectSelectionAdorner rectSelectionAdorner)
                rectSelectionAdorner.Update(_selectionStart, current);
            else if (Mode is GraphControlMode.LassoSelection &&
                     _selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner)
                lassoSelectionAdorner.AddPoint(current);
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
            var add = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            switch (Mode)
            {
                case GraphControlMode.RectSelection when _selectionAdorner is RectSelectionAdorner rectSelectionAdorner:
                    vm.ApplyRectSelection(rectSelectionAdorner.CurrentRect, add);
                    break;
                case GraphControlMode.LassoSelection
                    when _selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner:
                    vm.ApplyLassoSelection(lassoSelectionAdorner.Points, add);
                    break;
            }

            _selectionAdorner?.Clear();
        }
    }
}

public enum GraphControlMode
{
    RectSelection,
    LassoSelection,
    Pan
}