using System.Collections.Immutable;
using System.Windows.Input;
using System.Xml.Linq;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorWordViewModel : Bindable
    {
        public event EventHandler? Changed;
        readonly XElement word;
        VoiSonaTalkEditorPlayButtonStatus status = VoiSonaTalkEditorPlayButtonStatus.Idle;
        internal int WordIndex { get; }
        public string Text
        {
            get => word.Value;
            set => word.Value = value;
        }
        public bool IsChain
        {
            get => (string?)word.Attribute("chain") == "1";
        }
        public string Pronunciation
        {
            get => (string?)word.Attribute("pronunciation") ?? string.Empty;
            set
            {
                word.SetAttributeValue("pronunciation", value);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        public ImmutableList<VoiSonaTalkEditorMoraViewModel> Moras { get; }
        public bool IsChainable { get; }

        public ICommand JoinCommand { get; }
        public ICommand SplitCommand { get; }
        public ICommand PlayCommand { get; }
        public VoiSonaTalkEditorPlayButtonStatus Status { get => status; private set => Set(ref status, value); }

        public VoiSonaTalkEditorWordViewModel(XElement word, int wordIndex, bool isChainable, ICommand playCommand)
        {
            this.word = word;
            WordIndex = wordIndex;
            IsChainable = isChainable && !string.IsNullOrEmpty(Pronunciation);
            PlayCommand = playCommand;
            var moras = 
                Enumerable.Range(0, VoiSonaTalkEditorMoraViewModel.GetMoraList(word).Length)
                .Select(i => new VoiSonaTalkEditorMoraViewModel(word, i));
            Moras = [.. moras];
            foreach(var mora in Moras)
                mora.Changed += Child_Changed;

            JoinCommand = new ActionCommand(
                _ => true,
                _=>
                {
                    word.SetAttributeValue("chain", "1");

                    var acousticPhrase = word.Parent ?? throw new InvalidOperationException("SSML ノード構造が不正です。word 要素に親 acoustic_phrase 要素がありません。");
                    var groups = VoiSonaTalkTSMLNodeHelper.GetWordGroups(acousticPhrase);
                    var currentGroup = groups.First(g => g.Contains(word)).ToArray();

                    VoiSonaTalkTSMLNodeHelper.NormalizeChainedWordsAccent(currentGroup);
                    Changed?.Invoke(this, EventArgs.Empty);

                });
            SplitCommand = new ActionCommand(_ => true, _ =>
            {
                word.SetAttributeValue("chain", "0");

                var acousticPhrase = word.Parent ?? throw new InvalidOperationException("SSML ノード構造が不正です。word 要素に親 acoustic_phrase 要素がありません。");
                var groups = VoiSonaTalkTSMLNodeHelper.GetWordGroups(acousticPhrase).ToArray();
                var currentGroup = groups.First(g => g.Contains(word));

                var currentGroupIndex = Array.IndexOf(groups, currentGroup);
                var leftGroup = groups[currentGroupIndex - 1];

                VoiSonaTalkTSMLNodeHelper.NormalizeChainedWordsAccent(leftGroup);
                VoiSonaTalkTSMLNodeHelper.NormalizeChainedWordsAccent(currentGroup);
                Changed?.Invoke(this, EventArgs.Empty);
            });
        }

        private void Child_Changed(object? sender, EventArgs e)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void SetStatus(VoiSonaTalkEditorPlayButtonStatus status)
        {
            Status = status;
        }
    }
}
