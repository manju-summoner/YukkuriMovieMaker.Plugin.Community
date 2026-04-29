using YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;
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
            oldVm.RenameRequested -= OnRenameRequested;
        }

        if (e.NewValue is EffectTabManagerViewModel newVm)
        {
            newVm.BeginEdit += OnBeginEdit;
            newVm.EndEdit += OnEndEdit;
            newVm.RenameRequested += OnRenameRequested;
        }
    }

    private void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);
    private void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private void OnRenameRequested(Guid tabId)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var vm = DataContext as EffectTabManagerViewModel;
            if (vm is null) return;

            var tab = vm.Tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab is null) return;

            var container = TabListBox.ItemContainerGenerator.ContainerFromItem(tab) as ListBoxItem;
            if (container is null) return;

            var textBox = FindDescendantByName<TextBox>(container, "RenameTextBox");
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

    private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (!textBox.IsVisible) return;
        Dispatcher.InvokeAsync(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        });
    }

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
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };
        (VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement)?.RaiseEvent(eventArg);
    }
}