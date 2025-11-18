using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorViewModelConverter : MarkupExtension, IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not VoiSonaTalkVoicePronounce pronounce)
                return null;
            return new VoiSonaTalkEditorViewModel(pronounce);
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
