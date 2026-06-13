using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class RightClickSelectionBehavior : Behavior<ListBoxItem>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseRightButtonDown += AssociatedObject_PreviewMouseRightButtonDown;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseRightButtonDown -= AssociatedObject_PreviewMouseRightButtonDown;
        }

        private void AssociatedObject_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AssociatedObject.IsSelected)
                return;

            if (ItemsControl.ItemsControlFromItemContainer(AssociatedObject) is ListBox listBox)
            {
                listBox.UnselectAll();
                AssociatedObject.IsSelected = true;
                e.Handled = true;
            }
        }
    }
}
