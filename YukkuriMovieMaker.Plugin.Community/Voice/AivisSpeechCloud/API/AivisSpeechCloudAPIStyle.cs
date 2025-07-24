using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    public record AivisSpeechCloudAPIStyle(
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("icon_url")] string? IconUrl,
        [property: JsonProperty("local_id")] int LocalId,
        [property: JsonProperty("voice_samples")] List<AivisSpeechCloudAPIVoiceSample> VoiceSamples
        );
}