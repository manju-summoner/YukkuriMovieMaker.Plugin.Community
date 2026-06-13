using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.PiperPlus.API;

internal record PiperLanguageConfig(
    [property: JsonProperty("code")] string? Code,
    [property: JsonProperty("family")] string? Family,
    [property: JsonProperty("region")] string? Region,
    [property: JsonProperty("name_native")] string? NameNative,
    [property: JsonProperty("name_english")] string? NameEnglish,
    [property: JsonProperty("country_english")] string? CountryEnglish
);
