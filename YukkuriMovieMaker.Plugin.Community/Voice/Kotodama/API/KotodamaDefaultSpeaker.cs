using System;
using System.Collections.Generic;
using System.Text;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API
{
    record KotodamaDefaultSpeaker(string Id, string Name, KotodamaDefaultDecoration[] Decorations, string ContentRestrictions)
    {
        static readonly string CommonContentRestrictions = "※アダルト・暴力・差別・誹謗中傷、宗教・特定個人団体の非難などの利用を禁止します。";
        static readonly string CommonContentRestrictions2 = "※アダルト・性的なコンテンツ・暴力・差別・誹謗中傷・宗教・特定個人団体への非難などの利用を禁止します。";
        static readonly string JikkyoBabyContentRestrictions = "※アダルト・性的なコンテンツ・暴力・差別・誹謗中傷・宗教・特定個人団体への非難などの利用を禁止します。\r\n※広告やPRで使う場合は、事前にお問い合わせフォームまでご連絡ください。\r\nhttps://form.run/@kotodama-contact";
        static readonly string ShionContentRestrictions = "※アダルト・暴力・差別・誹謗中傷、宗教・特定個人団体の非難などの利用、その他不快なイメージを持たれてしまうものへの使用はNGを禁止します。";
        static readonly string KamiyamaContentRestrictions = "※アダルト・暴力・差別・誹謗中傷、宗教・特定個人団体の非難などの利用、その他、解説や紹介等の域に留まらない批判的な主張のための利用、政治に関する意見表明等の利用（公共施設など、福祉面の利用は可）を禁止します。";
        static readonly string CammyContentRestrictions = "※アダルト・暴力・差別・誹謗中傷、宗教・特定個人団体の非難などの利用及び不当な収益につながる利用を禁止します。";
        static readonly string IshigakiContentRestrictions = "※アダルト・性的なコンテンツ, 暴力, 差別, 誹謗中傷, 宗教, 特定個人団体への非難, 戦争など軍事事項に関わるもの、政党団体に関わるもの、高速再生などの利用を禁止します。";

        public static KotodamaDefaultSpeaker Atla { get; } = new("Atla", "アトラ", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Poporo { get; } = new("Poporo", "ポポロ", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        // IDが小文字で始まっているが、これで正しい
        public static KotodamaDefaultSpeaker JikkyoBaby { get; } = new("jikkyo_baby", "実況ベイビー", [.. KotodamaDefaultDecoration.CharacterDefaultDecorations, KotodamaDefaultDecoration.VeryHappy], JikkyoBabyContentRestrictions);
        public static KotodamaDefaultSpeaker Chunta { get; } = new("Chunta", "チュン太", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Mikko { get; } = new("Mikko", "みっ子", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Shion { get; } = new("Shion", "シオン", KotodamaDefaultDecoration.CharacterDefaultDecorations, ShionContentRestrictions);
        public static KotodamaDefaultSpeaker President { get; } = new("President", "社長", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Kodama { get; } = new("Kodama", "コダマ", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Kyusuke { get; } = new("Kyusuke", "キュウ介", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Kamiyama { get; } = new("Kamiyama", "カミヤマ", KotodamaDefaultDecoration.CharacterDefaultDecorations, KamiyamaContentRestrictions);
        public static KotodamaDefaultSpeaker Cammy { get; } = new("Cammy", "キャミー", KotodamaDefaultDecoration.CharacterDefaultDecorations, CammyContentRestrictions);
        public static KotodamaDefaultSpeaker Marlo { get; } = new("Marlo", "マーロ姫", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Kitahara { get; } = new(
            "Kitahara",
            "北原俊佑",
            KotodamaDefaultDecoration.ConcatEnglishDecorations([
                KotodamaDefaultDecoration.Neutral, KotodamaDefaultDecoration.Happy, KotodamaDefaultDecoration.Angry,
                KotodamaDefaultDecoration.Sad, KotodamaDefaultDecoration.Surprised]),
            CommonContentRestrictions);
        public static KotodamaDefaultSpeaker Ishigaki { get; } = new(
            "Ishigaki",
            "石垣真帆",
            KotodamaDefaultDecoration.ConcatEnglishDecorations([
                KotodamaDefaultDecoration.Neutral, KotodamaDefaultDecoration.Happy, KotodamaDefaultDecoration.Angry,
                KotodamaDefaultDecoration.Sad, KotodamaDefaultDecoration.Surprised, KotodamaDefaultDecoration.Laughing,
                KotodamaDefaultDecoration.Scared, KotodamaDefaultDecoration.Confused, KotodamaDefaultDecoration.Disgusted,
                KotodamaDefaultDecoration.Friendly, KotodamaDefaultDecoration.Interested, KotodamaDefaultDecoration.Crying]),
            IshigakiContentRestrictions);
        public static KotodamaDefaultSpeaker Suginaka = new("Suginaka", "次郎", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions2);
        public static KotodamaDefaultSpeaker Mel = new("Mel", "メル", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions2);
        public static KotodamaDefaultSpeaker Mofuhiko = new("Mofuhiko", "モフ彦", KotodamaDefaultDecoration.CharacterDefaultDecorations, CommonContentRestrictions2);

        public static KotodamaDefaultSpeaker[] AllSpeakers { get; } = [
            Atla, Poporo, JikkyoBaby, Chunta, Mikko,
            Shion, President, Kodama, Kyusuke, Kamiyama,
            Cammy, Marlo, Kitahara, Ishigaki, Suginaka,
            Mel, Mofuhiko];
    }
}
