using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

public class FilePathPortControlAttribute : PropertyControlBaseAttribute
{
    public override Type ControlType => typeof(FilePathPort);

    public string[] AllowExtension { get; set; } = [];
    public string Default { get; set; } = "";

    public override object GetDefaultValue()
    {
        return Default;
    }

    public List<(string Name, string[] Ext)> GetAllowExtensionList()
    {
        var list = new List<(string Name, string[] Ext)>();
        foreach (var entry in AllowExtension)
        {
            var parts = entry.Split('|', 2);
            if (parts.Length != 2) continue;
            list.Add((parts[0], parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries)));
        }

        return list;
    }
}