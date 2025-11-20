using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record SpeechSynthesisInformation(
        [property: JsonProperty("destination")] SpeechSynthesisDestination Destination,
        [property: JsonProperty("language")] string Language,
        [property: JsonProperty("output_file_path")] string? OutputFilePath,
        [property: JsonProperty("progress_percentage")] int ProgressPercentage,
        [property: JsonProperty("state")] RequestState State,
        [property: JsonProperty("text")] string Text,
        [property: JsonProperty("uuid")] string Uuid,
        [property: JsonProperty("analyzed_text")] string AnalyzedText,
        [property: JsonProperty("duration")] double Duration,
        [property: JsonProperty("global_parameters")] SpeechSynthesisGlobalParameters GlobalParameters,
        [property: JsonProperty("phonemes")] string[] Phonemes,
        [property: JsonProperty("phoneme_durations")] double[] PhonemeDurations,
        [property: JsonProperty("voice_name")] string VoiceName,
        [property: JsonProperty("voice_version")] string VoiceVersion
        );
}
