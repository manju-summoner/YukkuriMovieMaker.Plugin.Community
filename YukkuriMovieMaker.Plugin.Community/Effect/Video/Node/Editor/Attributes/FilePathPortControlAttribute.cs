using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class FilePathPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(FilePathPort);

    public List<(string Name, string[] Ext)> AllowExtension { get; set; } = new();
    public string Default { get; set; } = "";
}