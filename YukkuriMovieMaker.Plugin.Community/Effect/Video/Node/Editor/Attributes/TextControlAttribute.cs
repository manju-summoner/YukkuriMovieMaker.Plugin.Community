using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class TextPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(TextPort);

    /// <summary>
    ///     デフォルト値
    /// </summary>
    public string Default { get; set; } = "";
}