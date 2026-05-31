using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

internal record PiperModelConfig(
    [property: JsonProperty("num_speakers")] int NumSpeakers,
    [property: JsonProperty("speaker_id_map")] Dictionary<string, int>? SpeakerIdMap,
    [property: JsonProperty("dataset")] string? Dataset,
    [property: JsonProperty("language")] PiperLanguageConfig? Language,
    [property: JsonProperty("num_languages")] int NumLanguages,
    [property: JsonProperty("language_id_map")] Dictionary<string, int>? LanguageIdMap
);
