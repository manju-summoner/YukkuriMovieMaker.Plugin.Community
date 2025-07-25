using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    public record TagContract(
        [property: JsonProperty("name")] string Name
        );
}