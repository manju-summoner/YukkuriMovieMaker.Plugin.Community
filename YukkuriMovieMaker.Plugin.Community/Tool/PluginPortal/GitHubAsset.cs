using System.Text.Json.Serialization;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal
{
    internal class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}
