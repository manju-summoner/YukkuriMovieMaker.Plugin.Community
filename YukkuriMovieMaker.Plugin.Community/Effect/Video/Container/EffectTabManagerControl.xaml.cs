using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public partial class EffectTabManagerControl : UserControl, IPropertyEditorControl
{
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public EffectTabManagerControl()
    {
        InitializeComponent();
        DataContextChanged += EffectTabManagerControl_DataContextChanged;
    }

    public void SetProperties(ItemProperty[] itemProperties)
    {
        if (DataContext is IDisposable old)
            old.Dispose();
        DataContext = new EffectTabManagerViewModel(itemProperties);
    }

    public void ClearProperties()
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        DataContext = null;
    }

    private void EffectTabManagerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EffectTabManagerViewModel oldVm)
        {
            oldVm.BeginEdit -= OnBeginEdit;
            oldVm.EndEdit -= OnEndEdit;
        }

        if (e.NewValue is EffectTabManagerViewModel newVm)
        {
            newVm.BeginEdit += OnBeginEdit;
            newVm.EndEdit += OnEndEdit;
        }
    }

    private void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);
    private void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private void RenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not EffectTabItemViewModel tab) return;
        if (vm.CommitEditCommand.CanExecute(tab))
            vm.CommitEditCommand.Execute(tab);
    }

    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not EffectTabItemViewModel tab) return;

        if (e.Key == Key.Enter)
        {
            if (vm.CommitEditCommand.CanExecute(tab))
                vm.CommitEditCommand.Execute(tab);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (vm.CancelEditCommand.CanExecute(tab))
                vm.CancelEditCommand.Execute(tab);
            e.Handled = true;
        }
    }

    private void TabListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Handled) return;
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender
        };
        (VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement)?.RaiseEvent(eventArg);
    }

    private Point _dragStartPoint;

    private void TabListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void TabListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem == null) return;
            var tabVm = listBoxItem.DataContext as EffectTabItemViewModel;
            if (tabVm == null) return;

            DragDrop.DoDragDrop(listBoxItem, tabVm, DragDropEffects.Move);
        }
    }

    private void TabListBox_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(EffectTabItemViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var sourceTab = e.Data.GetData(typeof(EffectTabItemViewModel)) as EffectTabItemViewModel;
        var listBox = sender as ListBox;
        if (sourceTab == null || listBox == null) return;

        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        var targetTab = targetItem?.DataContext as EffectTabItemViewModel;

        var vm = DataContext as EffectTabManagerViewModel;
        if (vm == null) return;

        var pinnedCount = vm.Tabs.Count(t => t.IsPinned);

        var targetIndex = -1;
        if (targetItem != null)
        {
            targetIndex = listBox.ItemContainerGenerator.IndexFromContainer(targetItem);
            var pos = e.GetPosition(targetItem);
            if (pos.X > targetItem.ActualWidth / 2)
            {
                targetIndex++;
            }
        }
        else
        {
            targetIndex = vm.Tabs.Count;
        }

        if (sourceTab.IsPinned && targetIndex > pinnedCount)
        {
            e.Effects = DragDropEffects.None;
        }
        else if (!sourceTab.IsPinned && targetIndex < pinnedCount)
        {
            e.Effects = DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.Move;
        }
        e.Handled = true;
    }

    private void TabListBox_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(EffectTabItemViewModel))) return;

        var sourceTab = e.Data.GetData(typeof(EffectTabItemViewModel)) as EffectTabItemViewModel;
        var listBox = sender as ListBox;
        if (sourceTab == null || listBox == null) return;

        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        var vm = DataContext as EffectTabManagerViewModel;
        if (vm == null) return;

        var sourceIndex = vm.Tabs.IndexOf(sourceTab);
        var targetIndex = -1;

        if (targetItem != null)
        {
            targetIndex = listBox.ItemContainerGenerator.IndexFromContainer(targetItem);
            var pos = e.GetPosition(targetItem);
            if (pos.X > targetItem.ActualWidth / 2)
            {
                targetIndex++;
            }
        }
        else
        {
            targetIndex = vm.Tabs.Count;
        }

        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        if (sourceIndex != targetIndex && targetIndex >= 0 && targetIndex < vm.Tabs.Count)
        {
            var pinnedCount = vm.Tabs.Count(t => t.IsPinned);
            if ((sourceTab.IsPinned && targetIndex <= pinnedCount) || 
                (!sourceTab.IsPinned && targetIndex >= pinnedCount))
            {
                vm.MoveTab(sourceIndex, targetIndex);
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t)
                return t;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}