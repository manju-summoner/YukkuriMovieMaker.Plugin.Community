using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSVoices
    {
        public static readonly string[] Voices = 
            [
                "Zephyr",
                "Puck",
                "Charon",
                "Kore",
                "Fenrir",
                "Leda",
                "Orus",
                "Aoede",
                "Callirrhoe",
                "Autonoe",
                "Enceladus",
                "Iapetus",
                "Umbriel",
                "Algieba",
                "Despina",
                "Erinome",
                "Algenib",
                "Rasalgethi",
                "Laomedeia",
                "Achernar",
                "Alnilam",
                "Schedar",
                "Gacrux",
                "Pulcherrima",
                "Achird",
                "Zubenelgenubi",
                "Vindemiatrix",
                "Sadachbia",
                "Sadaltager",
                "Sulafat",
            ];

        /// <summary>
        /// プリセット30種は Google のボイス一覧では小文字の ID（kore）で載っているので、大文字と小文字を区別せずに判定する。YMM4 では既存のプロジェクトとの互換のため Kore のまま扱う
        /// </summary>
        public static bool IsPreset(string id) => Voices.Contains(id, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 話者一覧に出す声。手で入力した声（入力した順）→ プリセット30種（今の順）。
        /// プリセットと同じ ID・重複した ID の行は、同じ ID の話者が2つ並ばないように除く
        /// </summary>
        public static List<(string Id, string Name)> CreateSpeakerList(IEnumerable<GeminiTTSVoice> voices)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return
            [
                .. voices
                .Where(v => !string.IsNullOrEmpty(v.Id) && !IsPreset(v.Id) && seen.Add(v.Id))
                .Select(v => (v.Id!, string.IsNullOrWhiteSpace(v.Name) ? v.Id! : v.Name)),
                .. Voices.Select(v => (v, v)),
            ];
        }
    }
}
