namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal record ElevenLabsExpiringVoice(string Id, string Name, string ReplacementId, string ReplacementName);

    /// <summary>
    /// 2026-12-31 で使えなくなる ElevenLabs のデフォルトのボイスと、ElevenLabs が案内する置き換え先。
    /// 一覧 API の応答には期限切れになる声と置き換え先を見分ける項目が無いので、voice_id で判定する。
    /// 手入力で登録された声も API を呼ばずに判定できる。
    /// 出典: https://elevenlabs.io/docs/help-center/product/voices/my-voices/what-are-default-voices
    /// （表の各リンク先の default-voices?search= / voice-library?search= に voice_id がある。2026-10-18 取得）
    /// 置き換え先は Voice Library の声で、自分のボイスに保存するまで一覧 API には出ず、無料プランでは API で使えない。
    /// </summary>
    internal static class ElevenLabsDefaultVoices
    {
        public static IReadOnlyList<ElevenLabsExpiringVoice> ExpiringVoices { get; } =
        [
            new("CwhRBWXzGAHq8TQ4Fs17", "Roger", "gOupLcAkjEnguROwi4oS", "Darian - Warm Grounded Storyteller"),
            new("EXAVITQu4vr4xnSDxMaL", "Sarah", "OZ0L6eISlOejga3XjDFt", "Talia - Warm Soft Guide"),
            new("FGY2WhTYpPnrIDTdsKH5", "Laura", "WQP7cQUF5aAS6Axh5yaa", "Elara - Crisp Pro Narrator"),
            new("IKne3meq5aSn9XLyUdCD", "Charlie", "jSuBIjxMKhqIfb0wCK1F", "Baxter - Dry Calm Aussie"),
            new("JBFqnCBsd6RMkjVDRZzb", "George", "6WwXjDDEMyNmFG95zycZ", "Eldrin - Crisp British Baritone"),
            new("N2lVS1w4EtoT3dr4eOWO", "Callum", "cymHWdiF8WjUCg6vvFxx", "Kellan - Casual Friendly Speaker"),
            new("SAz9YHcvj6GT2YYXdXww", "River", "dvbL7qkNGZY1IqPGZAjM", "Elowen - Upbeat Modern Narrator"),
            new("SOYHLrjzK2X1ezoPC6cr", "Harry", "10NkTYmU7tSz3Kkl3Lex", "Kaelen - Amateur Warrior"),
            new("TX3LPaxmHKxFdv7VOQHJ", "Liam", "ktkP7Nsj67dw2zcplQYt", "Lawrence - Bright and Informative"),
            new("Xb7hH8MSUJpSbSDYk0k2", "Alice", "BFd5oBc2DDna33pSi4Gf", "Alicia - Polished Global Anchor"),
            new("XrExE9yKIg1WjnnlVkGX", "Matilda", "QtY3JBOUKEB5xzrRfOKc", "Maisie - Friendly Casual Neighbor"),
            new("bIHbv24MWmeRgasZH58o", "Will", "7QN34D2r3hCNwbOYIeK0", "Warren - Effortless and Cool"),
            new("cgSgspJ2msm6clMCkdW9", "Jessica", "g7LVvkPWALzPxOQbF6OE", "Jade - Upbeat and Natural"),
            new("cjVigY5qzO86Huf0OWal", "Eric", "l7kNoIfnJKPg7779LI2t", "Eddie - Helpful and Comforting"),
            new("iP95p4xoKVk53GoZ742B", "Chris", "AaOhDHYJ1XLZk74lXhdE", "Caleb - Trusted Guide"),
            new("nPczCjzI2devNBz1zQrb", "Brian", "8dEUmyPMdDdK91vboYih", "Sawyer - Midnight Storyteller"),
            new("onwK4e9ZLuTAKqWW03F9", "Daniel", "fnYMz3F5gMEDGMWcH1ex", "Finley - Articulate Anchor"),
            new("pFZP5JQG7iQjIQuC4Bku", "Lily", "22N9cF8z0o7y23njdyaY", "Florence - Atmospheric Storyteller"),
            new("pqHfZKP75CvOlQylNhV4", "Bill", "FrS6cKLB1wg4WYgPa9GW", "Wyatt - Seasoned Mentor"),
        ];

        static readonly Dictionary<string, ElevenLabsExpiringVoice> byId = ExpiringVoices.ToDictionary(x => x.Id, StringComparer.Ordinal);

        /// <summary>
        /// voice_id は大文字と小文字を区別する。読み上げでは表の ID がそのまま API に送られるので、前後の空白も取り除かずに比べる
        /// </summary>
        public static ElevenLabsExpiringVoice? Find(string? voiceId) =>
            voiceId is not null && byId.TryGetValue(voiceId, out var voice) ? voice : null;

        public static bool IsExpiring(string? voiceId) => Find(voiceId) is not null;

        public static string? GetWarning(string? voiceId) =>
            Find(voiceId) is { } voice ? string.Format(Texts.DefaultVoiceExpirationWarning, voice.Name, voice.ReplacementName) : null;
    }
}
