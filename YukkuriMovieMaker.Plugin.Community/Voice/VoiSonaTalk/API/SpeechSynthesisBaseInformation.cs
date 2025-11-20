using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record SpeechSynthesisBaseInformation(
        [property: JsonProperty("destination")] SpeechSynthesisDestination Destination,
        [property: JsonProperty("language")] string Language,
        [property: JsonProperty("output_file_path")] string? OutputFilePath,
        [property: JsonProperty("progress_percentage")] int ProgressPercentage,
        [property: JsonProperty("state")] RequestState State,
        [property: JsonProperty("text")] string Text,
        [property: JsonProperty("uuid")] string Uuid);
}
