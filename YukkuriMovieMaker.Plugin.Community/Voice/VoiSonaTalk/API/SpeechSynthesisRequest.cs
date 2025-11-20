using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record SpeechSynthesisRequest(
        [property: JsonProperty("analyzed_text")] string AnalyzedText,
        [property: JsonProperty("can_overwrite_file")] bool CanOverwriteFile,
        [property: JsonProperty("destination")] SpeechSynthesisDestination Destination,
        [property: JsonProperty("force_enqueue")] bool ForceEnqueue,
        [property: JsonProperty("global_parameters")] SpeechSynthesisGlobalParameters GlobalParameters,
        [property: JsonProperty("language")] string Language,
        [property: JsonProperty("output_file_path")] string? OutputFilePath,
        [property: JsonProperty("voice_name")] string VoiceName,
        [property: JsonProperty("voice_version")] string VoiceVersion
    );
}
