using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    public record SpeakerContract(
        [property: JsonProperty("aivm_speaker_uuid")] string AivmSpeakerUuid,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("icon_url")] string IconUrl,
        [property: JsonProperty("supported_languages")] List<string> SupportedLanguages,
        [property: JsonProperty("local_id")] int LocalId,
        [property: JsonProperty("styles")] List<StyleContract> Styles
        );
}