using System.Globalization;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    /// <summary>
    /// 値がエクスプローラーの項目かどうかを返す。
    /// 一覧の差し替えで切り離された項目のツールチップは Content が項目以外（null や切り離しの印）になるので、その判別に使う
    /// </summary>
    public sealed class IsExplorerItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is IExplorerItemViewModel;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
