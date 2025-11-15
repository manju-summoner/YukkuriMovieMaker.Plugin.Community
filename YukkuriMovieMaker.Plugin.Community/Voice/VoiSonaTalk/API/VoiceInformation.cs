using Newtonsoft.Json;
using System.Globalization;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record VoiceInformation(
        [property: JsonProperty("display_name")] DisplayName[] DisplayName,
        [property: JsonProperty("languages")] string[] Languages,
        [property: JsonProperty("voice_name")] string VoiceName,
        [property: JsonProperty("voice_version")] string VoiceVersion,
        [property: JsonProperty("default_style_weights")] double[] DefaultStyleWeights,
        [property: JsonProperty("style_names")] string[] StyleNames)
    {
        public string GetDisplayName(CultureInfo cultureInfo)
        {
            // 完全に一致するものを探す
            foreach (var displayName in DisplayName)
            {
                if (cultureInfo.Name.Equals(displayName.Language.Replace('_', '-'), StringComparison.OrdinalIgnoreCase))
                    return displayName.Name;
            }

            // 言語コードだけで一致するものを探す
            var langCode = cultureInfo.Name.Split('-')[0];
            foreach (var displayName in DisplayName)
            {
                if (langCode.Equals(displayName.Language.Split('_')[0], StringComparison.OrdinalIgnoreCase))
                    return displayName.Name;
            }

            // 一致する言語が存在しない場合は最初の表示名を返す
            if(DisplayName.Length > 0)
                return DisplayName[0].Name;

            // 表示名が存在しない場合は素の名前を返す
            return VoiceName;
        }
    }

}
