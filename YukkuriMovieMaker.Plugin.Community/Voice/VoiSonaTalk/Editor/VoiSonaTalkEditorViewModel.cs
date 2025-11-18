using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorViewModel : Bindable
    {
        readonly VoiSonaTalkVoicePronounce pronounce;
        XElement? currentRoot;

        ImmutableList<VoiSonaTalkEditorAcousticPhraseViewModel> acousticPhrases = [];
        public ImmutableList<VoiSonaTalkEditorAcousticPhraseViewModel> AcousticPhrases { get => acousticPhrases; set => Set(ref acousticPhrases, value); }

        public VoiSonaTalkEditorViewModel(VoiSonaTalkVoicePronounce pronounce)
        {
            this.pronounce = pronounce;
            WeakEventManager<VoiSonaTalkVoicePronounce, PropertyChangedEventArgs>.AddHandler(pronounce, nameof(pronounce.PropertyChanged), Pronounce_PropertyChanged);
            UpdateAcousticPhrases();
        }

        private void Pronounce_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(pronounce.TSML))
                UpdateAcousticPhrases();
        }

        void UpdateAcousticPhrases()
        {
            foreach(var phrase in AcousticPhrases)
                phrase.Changed -= Phrase_Changed;

            currentRoot = XDocument.Parse(pronounce.TSML ?? "<tsml></tsml>").Root;
            var acousticPhrases = currentRoot?.Elements("acoustic_phrase") ?? [];
            var phrases = acousticPhrases.Select((x, i) => new VoiSonaTalkEditorAcousticPhraseViewModel(x, i is 0)).ToImmutableList();
            foreach(var phrase in phrases)
                phrase.Changed += Phrase_Changed;
            AcousticPhrases = phrases;
        }

        private void Phrase_Changed(object? sender, EventArgs e)
        {
            if (currentRoot != null)
                pronounce.TSML = currentRoot.ToString(SaveOptions.DisableFormatting);
        }
    }
}
