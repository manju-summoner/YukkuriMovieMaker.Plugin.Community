using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public class RenameTextBoxBehavior : Behavior<TextBox>
    {
        public ICommand? CommitRenameCommand
        {
            get => (ICommand?)GetValue(CommitRenameCommandProperty);
            set => SetValue(CommitRenameCommandProperty, value);
        }
        public static readonly DependencyProperty CommitRenameCommandProperty =
            DependencyProperty.Register(nameof(CommitRenameCommand), typeof(ICommand), typeof(RenameTextBoxBehavior), new PropertyMetadata(null));

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
            AssociatedObject.IsVisibleChanged += AssociatedObject_IsVisibleChanged;
            AssociatedObject.LostFocus += AssociatedObject_LostFocus;
            AssociatedObject.PreviewKeyDown += AssociatedObject_PreviewKeyDown;
            AssociatedObject.PreviewMouseDown += AssociatedObject_PreviewMouseDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.Loaded -= AssociatedObject_Loaded;
            AssociatedObject.IsVisibleChanged -= AssociatedObject_IsVisibleChanged;
            AssociatedObject.LostFocus -= AssociatedObject_LostFocus;
            AssociatedObject.PreviewKeyDown -= AssociatedObject_PreviewKeyDown;
            AssociatedObject.PreviewMouseDown -= AssociatedObject_PreviewMouseDown;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            if (AssociatedObject.IsVisible)
                TryActivateRenaming();
        }

        private void AssociatedObject_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                TryActivateRenaming();
        }

        void TryActivateRenaming()
        {
            if (AssociatedObject.DataContext is not IExplorerSelectableItem item || !item.IsRenaming)
                return;

            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                if (!item.IsRenaming || AssociatedObject.DataContext != item)
                    return;

                AssociatedObject.Focus();

                var text = AssociatedObject.Text;
                var extIndex = text.LastIndexOf('.');
                var selectsNameOnly = item is IExplorerItemViewModel vm && vm.SelectsNameOnlyOnRename;

                if (extIndex > 0 && selectsNameOnly)
                    AssociatedObject.Select(0, extIndex);
                else
                    AssociatedObject.SelectAll();

            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void AssociatedObject_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AssociatedObject.DataContext is IExplorerSelectableItem item && item.IsRenaming)
                CommitRename(item);
        }

        private void AssociatedObject_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (AssociatedObject.DataContext is IExplorerSelectableItem item)
                    CommitRename(item);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (AssociatedObject.DataContext is IExplorerSelectableItem item)
                    item.IsRenaming = false;
                e.Handled = true;
            }
        }

        private void AssociatedObject_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!AssociatedObject.IsKeyboardFocusWithin)
            {
                AssociatedObject.Focus();
                e.Handled = true;
            }
        }

        private void CommitRename(IExplorerSelectableItem item)
        {
            var command = CommitRenameCommand;
            if (command?.CanExecute(item) == true)
                command.Execute(item);
            else
                item.IsRenaming = false;
        }
    }
}
