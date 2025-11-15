using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record RequestSpeechSynthesisResponse(
        [property: JsonProperty("uuid")] string Uuid);

}
