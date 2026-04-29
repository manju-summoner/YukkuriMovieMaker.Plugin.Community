using YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public partial class PresetManagerControl : UserControl, IPropertyEditorControl
{
    private const double MobileBreakpointWidth = 400.0;
    private const double MinControlHeight = 200.0;
    private const double MinGroupColumnWidthMobile = 0.0;
    private const double MinGroupColumnWidthDesktop = 120.0;
    private const double MaxGroupColumnWidthDesktop = 400.0;
    private const double MinGroupColumnWidthFallback = 50.0;
    private const string GroupDragFormat = "Container.GroupFormat";
    private const string PresetDragFormat = "Container.PresetFormat";

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private Point _dragStartPoint;
    private DropInsertionAdorner? _insertionAdorner;
    private AdornerLayer? _adornerLayer;

    public PresetManagerControl()
    {
        InitializeComponent();
        DataContextChanged += PresetManagerControl_DataContextChanged;
    }

    public void SetProperties(ItemProperty[] itemProperties)
    {
        DataContext = new PresetManagerViewModel(itemProperties);
    }

    public void ClearProperties()
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        DataContext = null;
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = ContainerSettings.Instance;
        Height = Math.Max(MinControlHeight, settings.ControlHeight);
        GroupColumn.Width = new GridLength(Math.Max(MinGroupColumnWidthFallback, settings.GroupColumnWidth));
    }

    private void PresetManagerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PresetManagerViewModel oldVm)
        {
            oldVm.BeginEdit -= OnBeginEdit;
            oldVm.EndEdit -= OnEndEdit;
            oldVm.GroupRenameRequested -= OnGroupRenameRequested;
            oldVm.PresetRenameRequested -= OnPresetRenameRequested;
        }
        if (e.NewValue is PresetManagerViewModel newVm)
        {
            newVm.BeginEdit += OnBeginEdit;
            newVm.EndEdit += OnEndEdit;
            newVm.GroupRenameRequested += OnGroupRenameRequested;
            newVm.PresetRenameRequested += OnPresetRenameRequested;
        }
    }

    private void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);
    private void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private void OnGroupRenameRequested(PresetGroup group)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var listBoxes = new[] { GroupListBox, MobileGroupListBox };
            foreach (var listBox in listBoxes)
            {
                var container = listBox?.ItemContainerGenerator.ContainerFromItem(group) as ListBoxItem;
                if (container is null) continue;
                var textBox = FindDescendantByName<TextBox>(container, "GroupRenameTextBox");
                if (textBox is null) continue;
                textBox.Focus();
                textBox.SelectAll();
                return;
            }
        });
    }

    private void OnPresetRenameRequested(PresetItemViewModel presetVm)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var container = PresetListBox.ItemContainerGenerator.ContainerFromItem(presetVm) as ListBoxItem;
            if (container is null) return;
            var textBox = FindDescendantByName<TextBox>(container, "PresetRenameTextBox");
            if (textBox is null) return;
            textBox.Focus();
            textBox.SelectAll();
        });
    }

    private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
                return element;

            var nested = FindDescendantByName<T>(child, name);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void GroupRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not PresetManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresetGroup group) return;
        vm.CommitGroupRename(group);
    }

    private void GroupRenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PresetManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresetGroup group) return;

        if (e.Key == Key.Enter)
        {
            vm.CommitGroupRename(group);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.CancelGroupRename(group);
            e.Handled = true;
        }
    }

    private void PresetRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not PresetManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresetItemViewModel presetVm) return;
        vm.CommitRenamePreset(presetVm);
    }

    private void PresetRenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PresetManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not PresetItemViewModel presetVm) return;

        if (e.Key == Key.Enter)
        {
            vm.CommitRenamePreset(presetVm);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.CancelRenamePreset(presetVm);
            e.Handled = true;
        }
    }

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var newHeight = ActualHeight + e.VerticalChange;
        if (newHeight >= MinControlHeight)
            Height = newHeight;
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var settings = ContainerSettings.Instance;
        settings.ControlHeight = Height;
        settings.Save();
    }

    private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var settings = ContainerSettings.Instance;
        settings.GroupColumnWidth = GroupColumn.Width.Value;
        settings.Save();
    }

    private void RootControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width < MobileBreakpointWidth)
        {
            MobileMenuButton.Visibility = Visibility.Visible;
            GroupPanel.Visibility = Visibility.Collapsed;
            GroupSplitter.Visibility = Visibility.Collapsed;
            GroupColumn.MinWidth = MinGroupColumnWidthMobile;
            GroupColumn.MaxWidth = MinGroupColumnWidthMobile;
            GroupColumn.Width = new GridLength(MinGroupColumnWidthMobile);
        }
        else
        {
            var settings = ContainerSettings.Instance;
            GroupColumn.MinWidth = MinGroupColumnWidthDesktop;
            GroupColumn.MaxWidth = MaxGroupColumnWidthDesktop;
            GroupColumn.Width = new GridLength(Math.Max(MinGroupColumnWidthDesktop, settings.GroupColumnWidth));
            GroupSplitter.Visibility = Visibility.Visible;
            GroupPanel.Visibility = Visibility.Visible;
            MobileMenuButton.Visibility = Visibility.Collapsed;
            MobileMenuButton.IsChecked = false;
        }
    }

    private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Handled) return;
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };
        (VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement)?.RaiseEvent(eventArg);
    }

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PresetManagerViewModel vm)
            vm.UpdateActionCommands();
    }

    private void GroupItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStartPoint = e.GetPosition(null);

    private void GroupItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup group) return;
        if (group.IsVirtual) return;

        DragDrop.DoDragDrop(item, new DataObject(GroupDragFormat, group), DragDropEffects.Move);
        RemoveAdorner();
    }

    private void GroupItem_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(GroupDragFormat)) return;
        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup group) return;
        if (group.IsVirtual) return;
        CreateAdorner(item);
    }

    private void GroupItem_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void GroupItem_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent(GroupDragFormat)) return;
        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup target) return;
        if (e.Data.GetData(GroupDragFormat) is PresetGroup source && DataContext is PresetManagerViewModel vm)
            vm.MoveGroup(source, target);
    }

    private void GroupItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not PresetGroup group) return;
        if (DataContext is PresetManagerViewModel vm && vm.RenameGroupCommand.CanExecute(group))
        {
            vm.RenameGroupCommand.Execute(group);
            e.Handled = true;
        }
    }

    private void PresetItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => _dragStartPoint = e.GetPosition(null);

    private void PresetItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is not ListBoxItem item || item.DataContext is not PresetItemViewModel preset) return;
        if (DataContext is PresetManagerViewModel vm && vm.IsCurrentGroupVirtual) return;

        DragDrop.DoDragDrop(item, new DataObject(PresetDragFormat, preset), DragDropEffects.Move);
        RemoveAdorner();
    }

    private void PresetItem_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(PresetDragFormat)) return;
        if (DataContext is PresetManagerViewModel vm && vm.IsCurrentGroupVirtual) return;
        if (sender is ListBoxItem item)
            CreateAdorner(item);
    }

    private void PresetItem_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void PresetItem_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent(PresetDragFormat)) return;
        if (sender is not ListBoxItem item || item.DataContext is not PresetItemViewModel target) return;
        if (e.Data.GetData(PresetDragFormat) is PresetItemViewModel source && DataContext is PresetManagerViewModel vm)
            vm.MovePreset(source, target);
    }

    private void PresetItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        if (sender is not ListBoxItem item) return;
        if (ItemsControl.ItemsControlFromItemContainer(item) is not ListBox listBox) return;
        if (listBox.ContextMenu is null) return;

        listBox.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void PresetItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item || item.DataContext is not PresetItemViewModel preset) return;
        if (DataContext is PresetManagerViewModel vm && vm.RenamePresetCommand.CanExecute(preset))
        {
            vm.RenamePresetCommand.Execute(preset);
            e.Handled = true;
        }
    }

    private void CreateAdorner(UIElement element)
    {
        RemoveAdorner();
        _adornerLayer = AdornerLayer.GetAdornerLayer(element);
        if (_adornerLayer is null) return;
        _insertionAdorner = new DropInsertionAdorner(element);
        _adornerLayer.Add(_insertionAdorner);
    }

    private void RemoveAdorner()
    {
        if (_adornerLayer is null || _insertionAdorner is null) return;
        _adornerLayer.Remove(_insertionAdorner);
        _insertionAdorner = null;
    }
}