using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class PortColorSettingAttribute(string color = nameof(Colors.SlateGray)) : Attribute
{
    public string Color { get; private set; } = color;
}