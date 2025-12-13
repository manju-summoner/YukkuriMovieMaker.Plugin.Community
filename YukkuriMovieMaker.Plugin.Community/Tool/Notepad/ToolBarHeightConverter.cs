using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using YamlDotNet.Core.Tokens;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Notepad
{
    internal class ToolBarHeightConverter : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return 0;
            var rate = values[0] as double? ?? 0;
            var size = values[1] as double? ?? 0;
            return 12 + (size - 12) * rate;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
