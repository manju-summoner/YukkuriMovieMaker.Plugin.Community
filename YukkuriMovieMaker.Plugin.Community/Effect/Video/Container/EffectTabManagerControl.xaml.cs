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

    private TabDragDropService? _dragDropService;

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
            oldVm.ConfirmationRequested -= OnConfirmationRequested;
            oldVm.BookmarkDialogRequested -= OnBookmarkDialogRequested;
        }

        _dragDropService?.Dispose();
        _dragDropService = null;

        if (e.NewValue is EffectTabManagerViewModel newVm)
        {
            newVm.BeginEdit += OnBeginEdit;
            newVm.EndEdit += OnEndEdit;
            newVm.ConfirmationRequested += OnConfirmationRequested;
            newVm.BookmarkDialogRequested += OnBookmarkDialogRequested;
            _dragDropService = new TabDragDropService(TabListBox, newVm);
        }
    }

    private void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

    private void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private void OnConfirmationRequested(object? sender, ConfirmationEventArgs e)
    {
        e.Confirmed = MessageBox.Show(e.Message, e.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void OnBookmarkDialogRequested(object? sender, BookmarkDialogEventArgs e)
    {
        var window = new BookmarkNameWindow(e.InitialName, e.IsEditMode)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow
        };
        window.ShowDialog();
        e.Result = window.Result;
        e.BookmarkName = window.BookmarkName ?? string.Empty;
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
            RoutedEvent = MouseWheelEvent,
            Source = sender
        };
        (VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement)?.RaiseEvent(eventArg);
    }

    private void ClearStashesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (vm.ClearStashesCommand.CanExecute(null))
            vm.ClearStashesCommand.Execute(null);
    }

    private void ClearBookmarksMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (vm.ClearBookmarksCommand.CanExecute(null))
            vm.ClearBookmarksCommand.Execute(null);
    }
}
