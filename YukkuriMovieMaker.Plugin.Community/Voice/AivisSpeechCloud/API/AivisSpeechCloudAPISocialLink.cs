using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    public record AivisSpeechCloudAPISocialLink(
        [property: JsonProperty("type")] string Type,
        [property: JsonProperty("url")] string Url
        );
}