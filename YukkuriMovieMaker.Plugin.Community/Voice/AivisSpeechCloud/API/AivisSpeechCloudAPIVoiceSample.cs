using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    public record AivisSpeechCloudAPIVoiceSample(
        [property: JsonProperty("audio_url")] string AudioUrl,
        [property: JsonProperty("transcript")] string Transcript
        );
}