using Newtonsoft.Json;
using System.Text.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record RequestTextAnalysisResponse(
        [property: JsonProperty("uuid")] string Uuid);

}
