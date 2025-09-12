using System.Text.Json.Serialization;

namespace YukkuriMovieMaker.Plugin.Community.Tool.PluginPortal.Model
{
    internal class ManjuboxReleaseInfo
    {
        [JsonPropertyName("user")]
        public string User { get; set; } = "";

        [JsonPropertyName("repo")]
        public string Repo { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("crawled_at")]
        public string CrawledAt { get; set; } = "";

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}
