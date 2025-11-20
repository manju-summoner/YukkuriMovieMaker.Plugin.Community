using System.Xml.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkTSMLNodeHelper
    {
        public static IEnumerable<IGrouping<int, XElement>> GetWordGroups(XElement acousticPhrase)
        {
            var sliderIndex = -1;
            var wordGroups =
                acousticPhrase
                .Elements("word")
                .Select(word => (word, sliderIndex: (string?)word.Attribute("chain") == "1" ? sliderIndex : ++sliderIndex))
                .GroupBy(x => x.sliderIndex, x => x.word);
            return wordGroups;
        }
        public static void NormalizeChainedWordsAccent(IEnumerable<XElement> words)
        {
            if(words.Skip(1).Any(w=>(string?)w.Attribute("chain") == "0"))
                throw new InvalidOperationException("未結合の word 要素が含まれています。");

            var totalHl = words.Select(x=>(string?)x.Attribute("hl") ?? string.Empty).DefaultIfEmpty(string.Empty).Aggregate(string.Concat);

            var firstHighIndex = totalHl.IndexOf('h');
            var lastHighIndex = totalHl.LastIndexOf('h');
            if (firstHighIndex is -1)
            {
                firstHighIndex = 0;
                lastHighIndex = 0;
            }
            else if(firstHighIndex is 0 && lastHighIndex == totalHl.Length - 1)
            {
                firstHighIndex = 1;
            }
            else if(firstHighIndex > 1)
            {
                firstHighIndex = 1;
            }
            int index = 0;
            foreach (var word in words)
            {
                var hl = (string?)word.Attribute("hl") ?? string.Empty;
                var hlList = hl.ToCharArray();
                for (int i = 0; i < hlList.Length; i++)
                {
                    if (index < firstHighIndex || index > lastHighIndex)
                        hlList[i] = 'l';
                    else
                        hlList[i] = 'h';
                    index++;
                }
                word.SetAttributeValue("hl", new string(hlList));
            }
        }
    }
}
