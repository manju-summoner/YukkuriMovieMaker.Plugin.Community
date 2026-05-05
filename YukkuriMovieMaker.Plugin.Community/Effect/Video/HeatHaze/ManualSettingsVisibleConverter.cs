using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.HeatHaze
{
    internal sealed class ManualSettingsVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is HeatHazeControlMode.Manual ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
