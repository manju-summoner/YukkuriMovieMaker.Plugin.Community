using Newtonsoft.Json;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    /// <summary>
    /// ボイス一覧 API（GET /v1beta/voices）の1件。
    /// 設定には選んだ声だけを保存するので、声を選ぶときにしか使わない項目は保存しない。
    /// </summary>
    internal class GeminiTTSVoiceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        public string Accent { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        [JsonIgnore]
        public string Pitch { get; set; } = string.Empty;
        [JsonIgnore]
        public string Persona { get; set; } = string.Empty;
        [JsonIgnore]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// ボイスデザイン（prompted）・ボイス複製（replicated）でユーザーが作った声
        /// </summary>
        [JsonIgnore]
        public bool IsCustom => !string.IsNullOrEmpty(Type) && Type != "prebuilt";

        [JsonIgnore]
        public string GenderText => Gender switch
        {
            "female" => Texts.Female,
            "male" => Texts.Male,
            "neutral" => Texts.Neutral,
            _ => Gender,
        };

        [JsonIgnore]
        public string KindText => IsCustom ? Texts.CustomVoice : Texts.LibraryVoice;

        /// <summary>
        /// 話者一覧での表示名。ライブラリの声は表示名が「Podcaster 1」のような汎用の名前なので、なまりと性別を添えて見分けられるようにする
        /// </summary>
        [JsonIgnore]
        public string SpeakerName
        {
            get
            {
                var name = string.IsNullOrEmpty(DisplayName) ? Id : DisplayName;
                if (IsCustom)
                    return string.Format(Texts.CustomVoiceSpeakerName, name);

                var details = string.Join("・", new[] { Accent, GenderText }.Where(x => !string.IsNullOrEmpty(x)));
                return string.IsNullOrEmpty(details) ? name : $"{name} ({details})";
            }
        }

        public GeminiTTSVoiceInfo CopyPersistentFields() => new()
        {
            Id = Id,
            DisplayName = DisplayName,
            LanguageCode = LanguageCode,
            Accent = Accent,
            Gender = Gender,
            Type = Type,
        };
    }
}
