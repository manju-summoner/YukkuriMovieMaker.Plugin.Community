using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API
{
    record KotodamaGenerateResponse(
        [property:JsonProperty("audios")] string[] Audios,
        [property:JsonProperty("durations")] double[] Durations,
        [property:JsonProperty("total_audio_seconds")] double TotalAudioSeconds,
        [property:JsonProperty("synthesis_id")] string SynthesisId);
}
