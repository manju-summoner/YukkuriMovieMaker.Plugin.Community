using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;

public partial class GradientEditor : UserControl, IPropertyEditorControl, IDisposable
{
    private static readonly SolidColorBrush MarkerIdleBrush;
    private static readonly SolidColorBrush MarkerActiveBrush;

    static GradientEditor()
    {
        MarkerIdleBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xAA, 0xAA, 0xAA));
        MarkerIdleBrush.Freeze();
        MarkerActiveBrush = new SolidColorBrush(Color.FromArgb(0xC0, 0x00, 0xBF, 0xFF));
        MarkerActiveBrush.Freeze();
    }

    public static readonly DependencyProperty GradientJsonProperty =
        DependencyProperty.Register(
            nameof(GradientJson),
            typeof(string),
            typeof(GradientEditor),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnGradientJsonChanged));

    private GradientEditorViewModel? _viewModel;
    private readonly Dictionary<Border, GradientColorStopViewModel> _markerToStop = [];

    private bool _isDragging;
    private Border? _draggingMarker;
    private GradientColorStopViewModel? _draggingStop;
    private double _dragStartMouseX;
    private double _dragStartStopPosition;

    private bool _isViewModelUpdating;
    private bool _isExternalUpdate;
    private bool _rebuildPending;
    private bool _disposed;

    public string GradientJson
    {
        get => (string)GetValue(GradientJsonProperty);
        set => SetValue(GradientJsonProperty, value);
    }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public GradientEditor()
    {
        InitializeComponent();
    }

    public void Initialize()
    {
        _viewModel = new GradientEditorViewModel();
        _viewModel.GradientJsonChanged += OnViewModelJsonChanged;
        ((INotifyCollectionChanged)_viewModel.Stops).CollectionChanged += OnStopsCollectionChanged;
        DataContext = _viewModel;

        _isExternalUpdate = true;
        try
        {
            _viewModel.LoadFromJson(GradientJson);
        }
        finally
        {
            _isExternalUpdate = false;
        }

        GradientCanvas.SizeChanged += OnCanvasSizeChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RebuildMarkers();
    }

    private static void OnGradientJsonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GradientEditor editor || editor._viewModel is null) return;
        if (editor._isViewModelUpdating) return;

        var json = e.NewValue as string ?? string.Empty;
        editor._isExternalUpdate = true;
        try
        {
            editor._viewModel.LoadFromJson(json);
        }
        finally
        {
            editor._isExternalUpdate = false;
        }

        editor.ScheduleRebuildMarkers();
    }

    private void OnViewModelJsonChanged(string json)
    {
        if (_isExternalUpdate) return;
        _isViewModelUpdating = true;
        try
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            GradientJson = json;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isViewModelUpdating = false;
        }
    }

    private void OnStopsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleRebuildMarkers();
    }

    private void ScheduleRebuildMarkers()
    {
        if (_rebuildPending) return;
        _rebuildPending = true;
        Dispatcher.InvokeAsync(RebuildMarkers, DispatcherPriority.Render);
    }

    private void RebuildMarkers()
    {
        _rebuildPending = false;

        foreach (var border in _markerToStop.Keys)
        {
            border.MouseLeftButtonDown -= OnMarkerMouseLeftButtonDown;
            border.MouseMove -= OnMarkerMouseMove;
            border.MouseLeftButtonUp -= OnMarkerMouseLeftButtonUp;
            border.MouseEnter -= OnMarkerMouseEnter;
            border.MouseLeave -= OnMarkerMouseLeave;
        }
        _markerToStop.Clear();
        GradientCanvas.Children.Clear();

        if (_viewModel is null) return;

        var canvasWidth = GradientCanvas.ActualWidth;
        var canvasHeight = GradientCanvas.ActualHeight;

        foreach (var stop in _viewModel.Stops)
        {
            var marker = CreateMarker(canvasHeight);
            marker.MouseLeftButtonDown += OnMarkerMouseLeftButtonDown;
            marker.MouseMove += OnMarkerMouseMove;
            marker.MouseLeftButtonUp += OnMarkerMouseLeftButtonUp;
            marker.MouseEnter += OnMarkerMouseEnter;
            marker.MouseLeave += OnMarkerMouseLeave;
            _markerToStop[marker] = stop;
            GradientCanvas.Children.Add(marker);
            PositionMarker(marker, stop.Position, canvasWidth);
        }
    }

    private static Border CreateMarker(double height)
    {
        return new Border
        {
            Width = 14,
            Height = height > 0 ? height : 22,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1.5),
            BorderBrush = MarkerIdleBrush,
            Cursor = Cursors.SizeWE,
        };
    }

    private static void PositionMarker(Border marker, float position, double canvasWidth)
    {
        if (canvasWidth > 0)
            Canvas.SetLeft(marker, position * (canvasWidth - marker.Width));
        Canvas.SetTop(marker, 0);
    }

    private void OnMarkerMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border marker && !_isDragging)
            marker.BorderBrush = MarkerActiveBrush;
    }

    private void OnMarkerMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border marker && _draggingMarker != marker)
            marker.BorderBrush = MarkerIdleBrush;
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var canvasWidth = GradientCanvas.ActualWidth;
        var canvasHeight = GradientCanvas.ActualHeight;

        if (_markerToStop.Count != (_viewModel?.Stops.Count ?? 0))
        {
            RebuildMarkers();
            return;
        }

        foreach (var (marker, stop) in _markerToStop)
        {
            if (canvasHeight > 0)
                marker.Height = canvasHeight;
            PositionMarker(marker, stop.Position, canvasWidth);
        }
    }

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging || _viewModel is null) return;

        var canvasWidth = GradientCanvas.ActualWidth;
        if (canvasWidth <= 0) return;

        var x = e.GetPosition(GradientCanvas).X;
        var position = (float)Math.Clamp(x / canvasWidth, 0.0, 1.0);
        var color = _viewModel.SampleColorAt(position);

        _viewModel.AddStopAt(position, color);
        e.Handled = true;
    }

    private void OnMarkerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border marker || !_markerToStop.TryGetValue(marker, out var stop)) return;
        if (_viewModel is null) return;

        _isDragging = true;
        _draggingMarker = marker;
        _draggingStop = stop;
        _dragStartMouseX = e.GetPosition(GradientCanvas).X;
        _dragStartStopPosition = stop.Position;

        marker.BorderBrush = MarkerActiveBrush;

        _viewModel.SuspendSerialization();
        marker.CaptureMouse();
        e.Handled = true;
    }

    private void OnMarkerMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _draggingMarker is null || _draggingStop is null) return;

        var canvasWidth = GradientCanvas.ActualWidth;
        if (canvasWidth <= 0) return;

        var currentX = e.GetPosition(GradientCanvas).X;
        var delta = currentX - _dragStartMouseX;
        var newPosition = (float)Math.Clamp(
            _dragStartStopPosition + delta / canvasWidth,
            0f,
            1f);

        _draggingStop.Position = newPosition;
        PositionMarker(_draggingMarker, newPosition, canvasWidth);
    }

    private void OnMarkerMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _draggingMarker is null) return;

        _draggingMarker.ReleaseMouseCapture();

        var released = _draggingMarker;
        _isDragging = false;
        _draggingMarker = null;
        _draggingStop = null;

        if (!released.IsMouseOver)
            released.BorderBrush = MarkerIdleBrush;

        _viewModel?.ResumeAndFinalizeDrag();
        e.Handled = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_viewModel is not null)
        {
            _viewModel.GradientJsonChanged -= OnViewModelJsonChanged;
            ((INotifyCollectionChanged)_viewModel.Stops).CollectionChanged -= OnStopsCollectionChanged;
            _viewModel.Dispose();
            _viewModel = null;
        }

        foreach (var border in _markerToStop.Keys)
        {
            border.MouseLeftButtonDown -= OnMarkerMouseLeftButtonDown;
            border.MouseMove -= OnMarkerMouseMove;
            border.MouseLeftButtonUp -= OnMarkerMouseLeftButtonUp;
            border.MouseEnter -= OnMarkerMouseEnter;
            border.MouseLeave -= OnMarkerMouseLeave;
        }
        _markerToStop.Clear();

        GradientCanvas.SizeChanged -= OnCanvasSizeChanged;
        Loaded -= OnLoaded;
    }
}
