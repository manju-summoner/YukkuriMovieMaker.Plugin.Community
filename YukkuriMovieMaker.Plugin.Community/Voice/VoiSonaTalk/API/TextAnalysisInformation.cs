using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record TextAnalysisInformation(
        [property: JsonProperty("progress_percentage")] int ProgressPercentage,
        [property: JsonProperty("state")] RequestState State,
        [property: JsonProperty("text")] string Text,
        [property: JsonProperty("uuid")] string Uuid,
        [property: JsonProperty("analyzed_text")] string AnalyzedText);
}
