using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View.Adorners;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.View;

public partial class GraphView
{
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(GraphControlMode),
            typeof(GraphView),
            new PropertyMetadata(GraphControlMode.RectSelection, OnModeChanged));

    private ConnectionAdorner? _connectionAdorner;
    private NotifyCollectionChangedEventHandler? _connectionsChangedHandler;
    private bool _ctrlRightPanning;

    private bool _isPanning;
    private bool _isSelecting;
    private Point _panStart;
    private SelectionAdornerBase? _selectionAdorner;
    private Point _selectionStart;
    private ObservableCollection<ConnectionViewModel>? _subscribedConnections;
    private bool _suppressNextContextMenu;

    public GraphView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        LostMouseCapture += OnLostMouseCapture;
    }

    public GraphControlMode Mode
    {
        get => (GraphControlMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (GraphView)d;
        var layer = AdornerLayer.GetAdornerLayer(control.RootGrid);
        if (layer is null) return;
        if (control._selectionAdorner != null)
            layer.Remove(control._selectionAdorner);

        switch ((GraphControlMode)e.NewValue)
        {
            case GraphControlMode.RectSelection:
                control._selectionAdorner = new RectSelectionAdorner(control.RootGrid);
                layer.Add(control._selectionAdorner);
                break;
            case GraphControlMode.LassoSelection:
                control._selectionAdorner = new LassoSelectionAdorner(control.RootGrid);
                layer.Add(control._selectionAdorner);
                break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var layer = AdornerLayer.GetAdornerLayer(RootGrid);
        if (layer is null) return;

        RemoveAdorners(layer);

        if (Mode == GraphControlMode.Pan) return;

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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeConnections();

        var layer = AdornerLayer.GetAdornerLayer(RootGrid);
        if (layer != null)
        {
            RemoveAdorners(layer);
        }
        else
        {
            _connectionAdorner = null;
            _selectionAdorner = null;
        }
    }

    private void RemoveAdorners(AdornerLayer layer)
    {
        if (_connectionAdorner != null)
        {
            layer.Remove(_connectionAdorner);
            _connectionAdorner = null;
        }

        if (_selectionAdorner != null)
        {
            layer.Remove(_selectionAdorner);
            _selectionAdorner = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeConnections();

        if (e.NewValue is GraphViewModel vm)
        {
            _connectionsChangedHandler = (_, _) =>
            {
                Dispatcher.BeginInvoke(new Action(() => { _connectionAdorner?.InvalidateVisual(); }),
                    DispatcherPriority.Loaded);
            };
            _subscribedConnections = vm.Connections;
            _subscribedConnections.CollectionChanged += _connectionsChangedHandler;
        }
    }

    private void UnsubscribeConnections()
    {
        if (_subscribedConnections != null && _connectionsChangedHandler != null)
            _subscribedConnections.CollectionChanged -= _connectionsChangedHandler;

        _subscribedConnections = null;
        _connectionsChangedHandler = null;
    }

    private static bool IsAdditiveSelectionModifier()
    {
        return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        if (vm.DraggingFromPort != null) return;

        _isSelecting = true;
        _selectionStart = e.GetPosition(this);

        if (!IsAdditiveSelectionModifier())
            vm.ClearSelection();
        if (Mode is GraphControlMode.RectSelection && _selectionAdorner is RectSelectionAdorner rectSelectionAdorner)
            rectSelectionAdorner.Update(_selectionStart, _selectionStart);
        else if (Mode is GraphControlMode.LassoSelection &&
                 _selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner)
            lassoSelectionAdorner.Begin(_selectionStart);
        else if (Mode is GraphControlMode.Pan)
        {
            _isPanning = true;
            _panStart = e.GetPosition(RootGrid);
        }

        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        if (_isPanning)
        {
            var current = e.GetPosition(RootGrid);
            var delta = current - _panStart;

            vm.PanX += delta.X;
            vm.PanY += delta.Y;

            _panStart = current;
            return;
        }

        if (vm.DraggingFromPort != null)
        {
            vm.TemporaryEndPoint = e.GetPosition(this);
            _connectionAdorner?.InvalidateVisual();
            return;
        }

        if (_isSelecting)
        {
            var current = e.GetPosition(this);
            if (Mode is GraphControlMode.RectSelection &&
                _selectionAdorner is RectSelectionAdorner rectSelectionAdorner)
                rectSelectionAdorner.Update(_selectionStart, current);
            else if (Mode is GraphControlMode.LassoSelection &&
                     _selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner)
                lassoSelectionAdorner.AddPoint(current);

            e.Handled = true;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        if (Mode == GraphControlMode.Pan && _isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }

        if (vm.DraggingFromPort != null)
        {
            vm.DraggingFromPort = null;
            vm.TemporaryEndPoint = null;
            _connectionAdorner?.InvalidateVisual();
            return;
        }

        if (_isSelecting)
        {
            var add = IsAdditiveSelectionModifier();
            var mode = Mode;
            var selectionAdorner = _selectionAdorner;

            _isSelecting = false;
            ReleaseMouseCapture();

            switch (mode)
            {
                case GraphControlMode.RectSelection when selectionAdorner is RectSelectionAdorner rectSelectionAdorner:
                    vm.ApplyRectSelection(rectSelectionAdorner.CurrentRect, add);
                    break;
                case GraphControlMode.LassoSelection
                    when selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner:
                    vm.ApplyLassoSelection(lassoSelectionAdorner.Points, add);
                    break;
            }

            selectionAdorner?.Clear();
        }
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _isPanning = false;
        _ctrlRightPanning = false;

        if (!_isSelecting) return;

        _isSelecting = false;

        if (DataContext is not GraphViewModel vm) return;

        var add = IsAdditiveSelectionModifier();
        var selectionAdorner = _selectionAdorner;

        switch (Mode)
        {
            case GraphControlMode.RectSelection when selectionAdorner is RectSelectionAdorner rectSelectionAdorner:
                vm.ApplyRectSelection(rectSelectionAdorner.CurrentRect, add);
                break;
            case GraphControlMode.LassoSelection when selectionAdorner is LassoSelectionAdorner lassoSelectionAdorner:
                vm.ApplyLassoSelection(lassoSelectionAdorner.Points, add);
                break;
        }

        selectionAdorner?.Clear();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not GraphViewModel vm) return;

        var mousePos = e.GetPosition(RootGrid);
        var oldZoom = vm.Zoom;

        var zoomFactor = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 1.05 : 1.1;
        var delta = e.Delta > 0 ? zoomFactor : 1.0 / zoomFactor;
        var newZoom = oldZoom * delta;

        if (newZoom < 0.1) newZoom = 0.1;
        if (newZoom > 5.0) newZoom = 5.0;

        vm.Zoom = newZoom;

        vm.PanX = mousePos.X - (mousePos.X - vm.PanX) * (newZoom / oldZoom);
        vm.PanY = mousePos.Y - (mousePos.Y - vm.PanY) * (newZoom / oldZoom);

        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        switch (e.ChangedButton)
        {
            case MouseButton.Right:
            {
                if (DataContext is not GraphViewModel vm) return;

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    _isPanning = true;
                    _ctrlRightPanning = true;
                    _panStart = e.GetPosition(RootGrid);
                    CaptureMouse();
                    e.Handled = true;
                    return;
                }

                var screen = e.GetPosition(this);
                var canvas = vm.TransformToCanvas(screen);
                vm.PendingContextPoint = canvas;
                break;
            }
            case MouseButton.Middle:
            {
                _isPanning = true;
                _panStart = e.GetPosition(RootGrid);
                CaptureMouse();

                e.Handled = true;
                break;
            }
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right && _ctrlRightPanning)
        {
            _isPanning = false;
            _ctrlRightPanning = false;
            _suppressNextContextMenu = true;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Middle) return;

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is GraphViewModel vm)
        {
            vm.Width = ActualWidth;
            vm.Height = ActualHeight;
        }
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_suppressNextContextMenu)
        {
            _suppressNextContextMenu = false;
            e.Handled = true;
            return;
        }

        if (DataContext is GraphViewModel vm)
        {
            var pos = Mouse.GetPosition(this);
            vm.PendingContextPoint = vm.TransformToCanvas(pos);
        }
    }
}

public enum GraphControlMode
{
    RectSelection,
    LassoSelection,
    Pan
}