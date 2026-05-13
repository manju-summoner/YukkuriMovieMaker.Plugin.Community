using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal static class FocusExtension
{
    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.RegisterAttached(
            "IsFocused",
            typeof(bool),
            typeof(FocusExtension),
            new UIPropertyMetadata(false, OnIsFocusedPropertyChanged));

    public static bool GetIsFocused(DependencyObject obj) => (bool)obj.GetValue(IsFocusedProperty);

    public static void SetIsFocused(DependencyObject obj, bool value) => obj.SetValue(IsFocusedProperty, value);

    private static void OnIsFocusedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        if ((bool)e.NewValue)
        {
            element.Dispatcher.InvokeAsync(() =>
            {
                element.Focus();
                if (element is TextBox textBox)
                {
                    textBox.SelectAll();
                }
            }, DispatcherPriority.Input);
        }
    }
}
