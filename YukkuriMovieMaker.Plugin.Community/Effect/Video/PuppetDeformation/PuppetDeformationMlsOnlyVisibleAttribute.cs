using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>
    /// 変形方式がMLSの場合のみプロパティを表示する。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class PuppetDeformationMlsOnlyVisibleAttribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(PuppetDeformationEffect.Algorithm))
            {
                Source = propertyOwner,
                Converter = new MlsToVisibilityConverter(),
            };
        }

        sealed class MlsToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                => value is PuppetDeformationAlgorithm algorithm && algorithm == PuppetDeformationAlgorithm.Mls
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }
}
