using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    public record AivisSpeechCloudAPITag(
        [property: JsonProperty("name")] string Name
        );
}