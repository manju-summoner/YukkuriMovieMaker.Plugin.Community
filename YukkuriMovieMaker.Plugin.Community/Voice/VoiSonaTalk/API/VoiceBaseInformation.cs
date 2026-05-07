using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record class VoiceBaseInformation(
        [property: JsonProperty("display_names")] DisplayName[] DisplayName,
        [property: JsonProperty("languages")] string[] Languages,
        [property: JsonProperty("voice_name")] string VoiceName,
        [property: JsonProperty("voice_version")] string VoiceVersion);
}
