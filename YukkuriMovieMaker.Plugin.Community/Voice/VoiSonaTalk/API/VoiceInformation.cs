using Newtonsoft.Json;
using System.Globalization;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.API
{
    internal record VoiceInformation(
        [property: JsonProperty("display_names")] DisplayName[] DisplayNames,
        [property: JsonProperty("languages")] string[] Languages,
        [property: JsonProperty("voice_name")] string VoiceName,
        [property: JsonProperty("voice_version")] string VoiceVersion,
        [property: JsonProperty("default_style_weights")] double[] DefaultStyleWeights,
        [property: JsonProperty("style_names")] string[] StyleNames)
    {
        public string GetDisplayName(CultureInfo cultureInfo)
        {
            if (DisplayNames is null || DisplayNames.Length is 0)
                return Texts.FailedToGetDisplayNameMessage;

            // 完全に一致するものを探す
            foreach (var displayName in DisplayNames)
            {
                if (cultureInfo.Name.Equals(displayName.Language.Replace('_', '-'), StringComparison.OrdinalIgnoreCase))
                    return displayName.Name;
            }

            // 言語コードだけで一致するものを探す
            var langCode = cultureInfo.Name.Split('-')[0];
            foreach (var displayName in DisplayNames)
            {
                if (langCode.Equals(displayName.Language.Split('_')[0], StringComparison.OrdinalIgnoreCase))
                    return displayName.Name;
            }

            // 一致する言語が存在しない場合は最初の表示名を返す
            if(DisplayNames.Length > 0)
                return DisplayNames[0].Name;

            // 表示名が存在しない場合は素の名前を返す
            return VoiceName;
        }
    }

}
