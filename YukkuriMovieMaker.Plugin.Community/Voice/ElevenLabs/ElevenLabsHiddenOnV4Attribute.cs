using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    //モデルはボイスごとではなくElevenLabsの設定で選ぶので、設定のTTSModelを見て表示を切り替える
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class ElevenLabsHiddenOnV4Attribute : Attribute, ICustomVisibilityAttribute2
    {
        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(ElevenLabsSettings.TTSModel))
            {
                Source = ElevenLabsSettings.Default,
                Converter = new ElevenLabsHiddenOnV4Converter(),
            };
        }
    }

    internal sealed class ElevenLabsHiddenOnV4Converter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return ElevenLabsTTSModels.IsV4(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
