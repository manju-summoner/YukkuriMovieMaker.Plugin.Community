using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class BoolToTextWrappingConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public override object ProvideValue(IServiceProvider serviceProvider)
            => this;
    }
}
