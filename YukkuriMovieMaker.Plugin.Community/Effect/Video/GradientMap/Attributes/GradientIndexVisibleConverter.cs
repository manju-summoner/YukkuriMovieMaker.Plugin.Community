using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

internal sealed class GradientIndexVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrEmpty(path)) return Visibility.Collapsed;

        var manifest = GradientTextureFactory.ReadManifest(path);
        return manifest.IsMultiple ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
