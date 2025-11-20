using System.Collections.Immutable;
using System.Xml.Linq;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorAcousticPhraseViewModel
    {
        public event EventHandler? Changed;
        readonly XElement acousticPhrase;
        public bool IsFirst { get; }

        public ImmutableList<VoiSonaTalkEditorWordViewModel> Words { get; }
        public ImmutableList<VoiSonaTalkEditorAccentSliderViewModel> AccentSliders { get; }

        public VoiSonaTalkEditorAcousticPhraseViewModel(XElement acousticPhrase, bool isFirst)
        {
            this.acousticPhrase = acousticPhrase;
            IsFirst = isFirst;

            var words = 
                acousticPhrase
                .Elements("word")
                .Select((x, i) => new VoiSonaTalkEditorWordViewModel(x, i > 0));
            Words = [.. words];
            foreach (var word in Words)
                word.Changed += Child_Changed;

            // chain属性が1で接続されているwordをグループ化してAccentSliderViewModelを作成
            var sliders = 
                VoiSonaTalkTSMLNodeHelper.GetWordGroups(acousticPhrase)
                .Select(group => new VoiSonaTalkEditorAccentSliderViewModel([.. group], group.Key == 0));
            AccentSliders = [.. sliders];
            foreach (var slider in AccentSliders)
                slider.Changed += Child_Changed;
        }

        void Child_Changed(object? sender, EventArgs e)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
