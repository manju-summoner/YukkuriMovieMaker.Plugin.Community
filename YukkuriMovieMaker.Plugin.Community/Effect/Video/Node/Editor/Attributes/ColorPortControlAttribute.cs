using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class ColorPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(ColorPort);

    public Color DefaultColor { get; set; } = Colors.White;
}