using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama
{
    internal class NullToFalseConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
