using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    // 音素長入力欄用。既定のdouble変換では空文字が変換エラーになり何も起きないため、
    // 空欄の確定を-1（自動算出）として通し、ピンを解除できるようにする
    internal sealed class VoiSonaTalkEditorPhonemeDurationTextConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double d ? d.ToString("F2", culture) : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
                return -1d;
            if (double.TryParse(text, NumberStyles.Float, culture, out var result))
                return result;
            // 解釈できない入力はNaNとして通し、VM側の非有限値ガードで表示を現在値へ巻き戻す
            // （Binding.DoNothingだとエラー表示もなく不正な文字列が残り続ける）
            return double.NaN;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
