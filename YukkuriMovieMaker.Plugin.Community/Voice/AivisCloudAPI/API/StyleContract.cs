using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    public record StyleContract(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("icon_url")] string? IconUrl,
        [property: JsonProperty("local_id")] int LocalId,
        [property: JsonProperty("voice_samples")] List<VoiceSampleContract> VoiceSamples
        );
}