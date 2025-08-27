using System.Text.Json.Serialization;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class GitHubRelease
    {
        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }
}
