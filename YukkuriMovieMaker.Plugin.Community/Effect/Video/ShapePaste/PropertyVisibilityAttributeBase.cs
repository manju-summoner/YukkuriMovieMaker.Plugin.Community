using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShapePaste
{
    internal abstract class PropertyVisibilityAttributeBase : Attribute, ICustomVisibilityAttribute2
    {
        protected abstract string SourcePropertyName { get; }

        protected abstract bool IsVisible(object? value);

        public Binding GetBinding(object item, object propertyOwner)
            => new(SourcePropertyName)
            {
                Source = item,
                Mode = BindingMode.OneWay,
                Converter = new VisibilityConverter(IsVisible),
            };

        private sealed class VisibilityConverter(Func<object?, bool> predicate) : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                => predicate(value) ? Visibility.Visible : Visibility.Collapsed;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }
}
