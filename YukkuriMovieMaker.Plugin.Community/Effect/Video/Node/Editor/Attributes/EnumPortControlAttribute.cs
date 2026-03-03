using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class EnumPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(EnumPort);

    public List<string> Items { get; set; } = new();
    public bool IsEditable { get; set; } = false;
    public int Default { get; set; } = 0;
}