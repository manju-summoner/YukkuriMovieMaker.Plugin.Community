using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class BoolPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(BoolPort);

    public bool Default { get; set; } = false;

    public override object GetDefaultValue()
    {
        return Default;
    }
}