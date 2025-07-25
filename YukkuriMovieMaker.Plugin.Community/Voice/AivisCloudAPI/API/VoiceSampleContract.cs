using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    public record VoiceSampleContract(
        [property: JsonProperty("audio_url")] string AudioUrl,
        [property: JsonProperty("transcript")] string Transcript
        );
}