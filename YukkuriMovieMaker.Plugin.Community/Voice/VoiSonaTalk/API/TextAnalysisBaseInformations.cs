using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record TextAnalysisBaseInformations(
        [property: JsonProperty("items")] TextAnalysisBaseInformation[] Items);

}
