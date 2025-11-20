using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record ErrorMessage(
        [property: JsonProperty("status_code")] int StatusCode,
        [property: JsonProperty("title")] string Title, 
        [property: JsonProperty("detail")] string Detail);
}
