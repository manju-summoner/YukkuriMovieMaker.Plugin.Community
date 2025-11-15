using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record class VoiceBaseInformations(
        [property: JsonProperty("items")] VoiceBaseInformation[] Items);

}
