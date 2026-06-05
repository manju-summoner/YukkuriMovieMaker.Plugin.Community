using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class ColorPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(ColorPort);

    public string DefaultColor { get; set; } = ColorStringConverter.ToString(Colors.White);

    public override object GetDefaultValue()
    {
        return ColorStringConverter.ToColor(DefaultColor);
    }
}