using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class EnumPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(EnumPort);

    public Type? Items { get; set; }
    public bool IsEditable { get; set; }
    public int Default { get; set; }

    public override object GetDefaultValue()
    {
        return Default;
    }
}