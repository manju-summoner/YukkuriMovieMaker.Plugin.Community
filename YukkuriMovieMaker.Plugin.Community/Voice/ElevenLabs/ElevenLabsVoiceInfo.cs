namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal enum ElevenLabsVoiceKind
    {
        /// <summary>
        /// クローン・ボイスデザインなどで自分が作った声
        /// </summary>
        MyVoice,
        /// <summary>
        /// Voice Library から自分のボイスに保存した声
        /// </summary>
        Library,
        Default,
    }

    /// <summary>
    /// ボイス一覧 API（GET /v2/voices）の1件。声を選ぶ画面でだけ使い、設定には保存しない
    /// </summary>
    internal class ElevenLabsVoiceInfo
    {
        public string VoiceId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public bool? IsOwner { get; init; }
        public string Gender { get; init; } = string.Empty;
        public string Age { get; init; } = string.Empty;
        public string Accent { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// is_owner が返らないときは、デフォルト（premade）以外を自分の声とみなす
        /// </summary>
        public ElevenLabsVoiceKind Kind =>
            Category == "premade" ? ElevenLabsVoiceKind.Default
            : IsOwner == false ? ElevenLabsVoiceKind.Library
            : ElevenLabsVoiceKind.MyVoice;

        public string KindText => Kind switch
        {
            ElevenLabsVoiceKind.MyVoice => Texts.MyVoice,
            ElevenLabsVoiceKind.Library => Texts.LibraryVoice,
            _ => Texts.DefaultVoice,
        };

        public string SpeakerName => string.IsNullOrWhiteSpace(Name) ? VoiceId : Name;
    }
}
