using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record DisplayName(
        [property: JsonProperty("language")] string Language, 
        [property: JsonProperty("name")] string Name);
}
