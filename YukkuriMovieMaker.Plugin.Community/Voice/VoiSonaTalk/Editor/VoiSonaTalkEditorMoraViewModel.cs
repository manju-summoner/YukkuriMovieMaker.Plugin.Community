using System.Collections.Immutable;
using System.Xml.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorMoraViewModel(XElement word, int moraIndex) : Bindable
    {
        public event EventHandler? Changed;
        static readonly char[] youonFirsts = [ 'キ', 'ギ', 'シ', 'ジ', 'チ', 'ニ', 'ヒ', 'ビ', 'ピ', 'ミ', 'リ' ];
        static readonly char[] youonSeconds = [ 'ェ', 'ャ', 'ュ', 'ョ'];
        static readonly string[] extraYouonPairs = [
            "イェ",
                   "ウィ","ウェ","ウォ",
            "ヴァ","ヴィ","ヴェ","ヴォ",
            "ヴャ","ヴュ","ヴョ",
            "スィ",
            "ズィ",
            "ツァ","ツィ","ツェ","ツォ",
            "ティ",
            "ディ",
            "テャ","テュ","テョ",
            "デャ","デュ","デョ",
            "トゥ",
            "ドゥ",
            "ファ","フィ","フェ","フォ",
            "フャ","フュ","フョ"
        ];

        public string Text => GetMoraList(word).Skip(moraIndex).FirstOrDefault() ?? string.Empty;

        public bool IsHigh
        {
            get => word.Attribute("hl")?.Value?.Skip(moraIndex).FirstOrDefault() is 'h';
            set
            {
                var hl = (string?)word.Attribute("hl") ?? string.Empty;
                var hlList = hl.ToCharArray();
                if(moraIndex <  hlList.Length)
                    hlList[moraIndex] = value ? 'h' : 'l';
                word.SetAttributeValue("hl", new string(hlList));
                OnPropertyChanged(nameof(IsHigh));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public static string[] GetMoraList(XElement word)
        {
            var pronunciation = (string?)word.Attribute("pronunciation") ?? string.Empty;
            var moraList = new List<string>();
            for (int i = 0; i < pronunciation.Length; i++)
            {
                char c = pronunciation[i];
                if (i + 1 < pronunciation.Length && (IsYouonCombination(c, pronunciation[i + 1]) || pronunciation[i+1] is '’'))
                {
                    // 拗音・「'」は一つのモーラとして扱う
                    moraList.Add(new string([c, pronunciation[i + 1]]));
                    i++; // 次の文字はすでに処理済みなのでスキップ
                }
                else
                {
                    moraList.Add(c.ToString());
                }
            }
            return [.. moraList];
        }

        static bool IsYouonCombination(char first, char second)
        {
            // 拗音の組み合わせを判定する
            return
                (youonFirsts.Contains(first) && youonSeconds.Contains(second))
                || extraYouonPairs.Any(pair=> pair[0] == first && pair[1] == second);
        }
    }
}
