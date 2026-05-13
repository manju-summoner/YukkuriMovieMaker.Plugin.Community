using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class TabDragDropService : IDisposable
{
    private const string DragFormat = "YukkuriMovieMaker.Plugin.Community.Effect.Video.Container.TabDrag";
    private const double IndicatorWidth = 3.0;
    private static readonly System.Windows.Media.Brush IndicatorBrush = SystemColors.HighlightBrush;

    private readonly ListBox _listBox;
    private readonly EffectTabManagerViewModel _viewModel;

    private Point _dragStartPoint;
    private bool _isDragStartPending;
    private EffectTabItemViewModel? _pendingDragItem;

    private DropIndicatorAdorner? _dropIndicator;
    private int _currentDropIndex = -1;
    private bool _disposed;

    public TabDragDropService(ListBox listBox, EffectTabManagerViewModel viewModel)
    {
        _listBox = listBox;
        _viewModel = viewModel;
        Attach();
    }

    private void Attach()
    {
        _listBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        _listBox.PreviewMouseMove += OnPreviewMouseMove;
        _listBox.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        _listBox.DragEnter += OnDragEnter;
        _listBox.DragOver += OnDragOver;
        _listBox.DragLeave += OnDragLeave;
        _listBox.Drop += OnDrop;
        _listBox.AllowDrop = true;
    }

    private void Detach()
    {
        _listBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        _listBox.PreviewMouseMove -= OnPreviewMouseMove;
        _listBox.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        _listBox.DragEnter -= OnDragEnter;
        _listBox.DragOver -= OnDragOver;
        _listBox.DragLeave -= OnDragLeave;
        _listBox.Drop -= OnDrop;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = GetTabItemViewModelAt(e.GetPosition(_listBox));
        if (item == null) return;

        _dragStartPoint = e.GetPosition(_listBox);
        _isDragStartPending = true;
        _pendingDragItem = item;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragStartPending || _pendingDragItem == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ResetDragPending();
            return;
        }

        var current = e.GetPosition(_listBox);
        var delta = current - _dragStartPoint;

        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        BeginDrag(_pendingDragItem);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ResetDragPending();
    }

    private void ResetDragPending()
    {
        _isDragStartPending = false;
        _pendingDragItem = null;
    }

    private void BeginDrag(EffectTabItemViewModel item)
    {
        ResetDragPending();
        var data = new DataObject(DragFormat, item);
        _listBox.ReleaseMouseCapture();
        DragDrop.DoDragDrop(_listBox, data, DragDropEffects.Move);
        HideDropIndicator();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (!IsOurDragFormat(e))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        UpdateDropIndicator(e.GetPosition(_listBox));
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!IsOurDragFormat(e))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        UpdateDropIndicator(e.GetPosition(_listBox));
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        var pos = e.GetPosition(_listBox);
        var bounds = new Rect(_listBox.RenderSize);
        if (!bounds.Contains(pos))
            HideDropIndicator();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        HideDropIndicator();

        if (!IsOurDragFormat(e)) return;
        var draggedItem = e.Data.GetData(DragFormat) as EffectTabItemViewModel;
        if (draggedItem == null) return;

        var dropIndex = CalculateDropIndex(e.GetPosition(_listBox));
        if (dropIndex < 0) return;

        var sourceIndex = _viewModel.Tabs.IndexOf(draggedItem);
        if (sourceIndex < 0) return;

        if (dropIndex == sourceIndex || dropIndex == sourceIndex + 1) return;

        _viewModel.MoveTabToIndexCommand.Execute(new MoveTabToIndexParameter(draggedItem, dropIndex));
        e.Handled = true;
    }

    private void UpdateDropIndicator(Point mousePosition)
    {
        var newIndex = CalculateDropIndex(mousePosition);
        if (newIndex == _currentDropIndex) return;

        _currentDropIndex = newIndex;
        ShowDropIndicator(newIndex);
    }

    private void ShowDropIndicator(int insertIndex)
    {
        var layer = AdornerLayer.GetAdornerLayer(_listBox);
        if (layer == null) return;

        RemoveAdorner(layer);

        var x = CalculateIndicatorX(insertIndex);
        if (x < 0) return;

        _dropIndicator = new DropIndicatorAdorner(_listBox, x, IndicatorWidth, IndicatorBrush);
        layer.Add(_dropIndicator);
    }

    private void HideDropIndicator()
    {
        _currentDropIndex = -1;
        var layer = AdornerLayer.GetAdornerLayer(_listBox);
        if (layer != null) RemoveAdorner(layer);
    }

    private void RemoveAdorner(AdornerLayer layer)
    {
        if (_dropIndicator == null) return;
        layer.Remove(_dropIndicator);
        _dropIndicator = null;
    }

    private int CalculateDropIndex(Point mousePosition)
    {
        var items = _listBox.Items;
        if (items.Count == 0) return 0;

        for (int i = 0; i < items.Count; i++)
        {
            var container = _listBox.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
            if (container == null) continue;

            var bounds = GetBoundsRelativeToListBox(container);
            var midX = bounds.Left + bounds.Width / 2.0;

            if (mousePosition.X < midX) return i;
        }

        return items.Count;
    }

    private double CalculateIndicatorX(int insertIndex)
    {
        var items = _listBox.Items;
        if (items.Count == 0) return 0;

        if (insertIndex < items.Count)
        {
            var container = _listBox.ItemContainerGenerator.ContainerFromIndex(insertIndex) as FrameworkElement;
            if (container == null) return -1;
            var bounds = GetBoundsRelativeToListBox(container);
            return bounds.Left;
        }
        else
        {
            var container = _listBox.ItemContainerGenerator.ContainerFromIndex(items.Count - 1) as FrameworkElement;
            if (container == null) return -1;
            var bounds = GetBoundsRelativeToListBox(container);
            return bounds.Right;
        }
    }

    private Rect GetBoundsRelativeToListBox(FrameworkElement element)
    {
        var transform = element.TransformToAncestor(_listBox);
        var origin = transform.Transform(new Point(0, 0));
        return new Rect(origin, element.RenderSize);
    }

    private EffectTabItemViewModel? GetTabItemViewModelAt(Point listBoxPosition)
    {
        var hit = VisualTreeHelper.HitTest(_listBox, listBoxPosition);
        if (hit == null) return null;

        var element = hit.VisualHit as DependencyObject;
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is EffectTabItemViewModel vm)
                return vm;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static bool IsOurDragFormat(DragEventArgs e) => e.Data.GetDataPresent(DragFormat);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        HideDropIndicator();
        Detach();
    }
}
