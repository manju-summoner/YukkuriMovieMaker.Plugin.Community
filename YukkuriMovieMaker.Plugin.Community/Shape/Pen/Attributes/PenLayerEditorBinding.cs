using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Attributes
{
    internal static class PenLayerEditorBinding
    {
        public static void SetEnabled(FrameworkElement control, ItemProperty[] itemProperties, string path)
        {
            if (itemProperties.FirstOrDefault()?.PropertyOwner is not PenLayer layer)
            {
                ClearEnabled(control);
                return;
            }

            BindingOperations.SetBinding(control, UIElement.IsEnabledProperty, new Binding(path)
            {
                Source = layer,
                Mode = BindingMode.OneWay,
            });
        }

        public static void ClearEnabled(FrameworkElement control)
            => BindingOperations.ClearBinding(control, UIElement.IsEnabledProperty);
    }
}
