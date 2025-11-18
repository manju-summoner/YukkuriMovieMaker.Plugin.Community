using System.Xml.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorAccentSliderViewModel(XElement[] words, bool isFirst) : Bindable
    {
        public event EventHandler? Changed;
        public bool IsFirst => isFirst;
        public int AccentPosition
        {
            get => words
                    .SelectMany(w => w.Attribute("hl")?.Value ?? string.Empty)
                    .Select((c, i) => (isHight: c is 'h', i))
                    .Where(x => x.isHight)
                    .Select(x => x.i)
                    .LastOrDefault();
            set
            {
                int index = 0;
                foreach (var word in words)
                {
                    var hl = (word.Attribute("hl")?.Value ?? string.Empty).ToCharArray();
                    for (int i = 0; i < hl.Length; i++)
                    {
                        hl[i] = (value < index || (index is 0 && value != 0)) ? 'l' : 'h';
                        index++;
                    }
                    word.SetAttributeValue("hl", new string(hl));
                }
                OnPropertyChanged(nameof(AccentPosition));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        public int AccentLength
        {
            get => words.Sum(w => (w.Attribute("hl")?.Value ?? string.Empty).Length);
        }
        public int AccentMax => AccentLength - 1;
        public double Width => AccentLength * 26;
    }
}
