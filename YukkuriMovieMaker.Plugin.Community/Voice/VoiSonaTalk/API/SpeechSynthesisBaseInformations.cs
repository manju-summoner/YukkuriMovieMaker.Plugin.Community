using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record SpeechSynthesisBaseInformations(
        [property: JsonProperty("items")] SpeechSynthesisBaseInformation[] Items);
}
