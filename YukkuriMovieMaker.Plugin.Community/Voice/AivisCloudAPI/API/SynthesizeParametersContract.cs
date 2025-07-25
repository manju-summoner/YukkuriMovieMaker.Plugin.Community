using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    /*
    {
    "model_uuid": "e630cd00-a263-42ca-9b5b-b1fe718847fc",
    "speaker_uuid": "3e4d4d59-a274-4c38-a869-0b609d76ec3e",
    "style_id": 31,
    "style_name": "string",
    "user_dictionary_uuid": "9f1ebfa9-9a03-4ec9-8d9c-7cd19ec7e0d0",
    "text": "string",
    "use_ssml": true,
    "language": "ja",
    "speaking_rate": 1,
    "emotional_intensity": 1,
    "tempo_dynamics": 1,
    "pitch": 0,
    "volume": 1,
    "leading_silence_seconds": 0.1,
    "trailing_silence_seconds": 0.1,
    "line_break_silence_seconds": 0.4,
    "output_format": "wav",
    "output_bitrate": 8,
    "output_sampling_rate": 8000,
    "output_audio_channels": "mono"
    }
    */
    internal record SynthesizeParametersContract(
        [property: JsonProperty("model_uuid")] string ModelUuid, //required
        [property: JsonProperty("speaker_uuid")] string? SpeakerUuid,
        [property: JsonProperty("style_id")] int? StyleId,
        [property: JsonProperty("style_name")] string? StyleName,
        [property: JsonProperty("user_dictionary_uuid")] string? UserDictionaryUuid,
        [property: JsonProperty("text")] string Text, //required
        [property: JsonProperty("use_ssml")] bool? UseSsml,
        [property: JsonProperty("language")] string? Language,
        [property: JsonProperty("speaking_rate")] double? SpeakingRate,
        [property: JsonProperty("emotional_intensity")] double? EmotionalIntensity,
        [property: JsonProperty("tempo_dynamics")] double? TempoDynamics,
        [property: JsonProperty("pitch")] double? Pitch,
        [property: JsonProperty("volume")] double? Volume,
        [property: JsonProperty("leading_silence_seconds")] double? LeadingSilenceSeconds,
        [property: JsonProperty("trailing_silence_seconds")] double? TrailingSilenceSeconds,
        [property: JsonProperty("line_break_silence_seconds")] double? LineBreakSilenceSeconds,
        [property: JsonProperty("output_format")] string? OutputFormat,
        [property: JsonProperty("output_bitrate")] int? OutputBitrate,
        [property: JsonProperty("output_sampling_rate")] int? OutputSamplingRate,
        [property: JsonProperty("output_audio_channels")] string? OutputAudioChannels
        );
}
