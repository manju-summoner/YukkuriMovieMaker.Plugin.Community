using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    public record SocialLinkContract(
        [property: JsonProperty("type")] string Type,
        [property: JsonProperty("url")] string Url
        );
}