using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Equalizer2
{
    public partial class BandsEditor : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public BandsEditor()
        {
            InitializeComponent();
            DataContextChanged += BandsEditor_DataContextChanged;
        }

        private void BandsEditor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is BandsEditorViewModel oldVm)
            {
                oldVm.BeginEdit -= Vm_BeginEdit;
                oldVm.EndEdit -= Vm_EndEdit;
            }
            if (e.NewValue is BandsEditorViewModel newVm)
            {
                newVm.BeginEdit += Vm_BeginEdit;
                newVm.EndEdit += Vm_EndEdit;
            }
        }

        private void Vm_BeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, e);

        private void Vm_EndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, e);

        private void PropertiesEditor_BeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, e);

        private void PropertiesEditor_EndEdit(object? sender, EventArgs e)
        {
            (DataContext as BandsEditorViewModel)?.CopyToOtherItems();
            EndEdit?.Invoke(this, e);
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (Keyboard.Modifiers != ModifierKeys.None)
            {
                if (sender is ListBox listBox)
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
                    if (scrollViewer is not null)
                    {
                        int lines = SystemParameters.WheelScrollLines;
                        for (int i = 0; i < lines; i++)
                        {
                            if (e.Delta > 0)
                                scrollViewer.LineUp();
                            else
                                scrollViewer.LineDown();
                        }
                    }
                }
                return;
            }

            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            RaiseEvent(args);
        }

        private static T? FindVisualChild<T>(DependencyObject element) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child is T target) return target;
                var found = FindVisualChild<T>(child);
                if (found is not null) return found;
            }
            return null;
        }
    }
}
