using YamlDotNet.Serialization;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class PluginInfo
    {
        [YamlMember(Alias = "type")]
        public string Type { get; set; }

        [YamlMember(Alias = "date")]
        public DateTime Date { get; set; }

        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "url")]
        public string Url { get; set; }

        [YamlMember(Alias = "author")]
        public string Author { get; set; }

        [YamlMember(Alias = "authorUrl")]
        public string AuthorUrl { get; set; }

        [YamlMember(Alias = "description")]
        public string Description { get; set; }

        [YamlMember(Alias = "links")]
        public List<string> Links { get; set; }

        [YamlMember(Alias = "isEnabled")]
        public bool IsEnabled { get; set; } = true;
    }
}
