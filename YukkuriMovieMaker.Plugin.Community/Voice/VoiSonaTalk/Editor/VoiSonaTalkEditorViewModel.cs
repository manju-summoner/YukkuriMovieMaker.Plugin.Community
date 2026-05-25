using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Media;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkEditorViewModel : Bindable
    {
        readonly VoiSonaTalkVoicePronounce pronounce;
        readonly SemaphoreSlim playbackControlSemaphore = new(1, 1);
        XElement? currentRoot;
        IAudioStream? stream;
        AudioPlayer? player;
        bool isPlaying = false;
        bool isBusy = false;
        readonly ICommand playFromCommand;

        ImmutableList<VoiSonaTalkEditorAcousticPhraseViewModel> acousticPhrases = [];
        public ImmutableList<VoiSonaTalkEditorAcousticPhraseViewModel> AcousticPhrases { get => acousticPhrases; set => Set(ref acousticPhrases, value); }

        ImmutableList<VoiSonaTalkEditorPhonemeViewModel> phonemes = [];
        public ImmutableList<VoiSonaTalkEditorPhonemeViewModel> Phonemes { get => phonemes; set => Set(ref phonemes, value); }

        public bool IsPlaying { get => isPlaying; set => Set(ref isPlaying, value); }
        public bool IsBusy { get => isBusy; set => Set(ref isBusy, value); }
        public ICommand TogglePlayCommand { get; }
        public IEditorInfo? Info { get; set; }

        public VoiSonaTalkEditorViewModel(VoiSonaTalkVoicePronounce pronounce)
        {
            this.pronounce = pronounce;
            WeakEventManager<VoiSonaTalkVoicePronounce, PropertyChangedEventArgs>.AddHandler(pronounce, nameof(pronounce.PropertyChanged), Pronounce_PropertyChanged);
            playFromCommand = new ActionCommand(
                _ => true,
                async arg => await PlayPauseAsync(arg as VoiSonaTalkEditorWordViewModel));
            TogglePlayCommand = new ActionCommand(
                _ => true,
                async _ => await PlayPauseAsync(null));

            UpdateAcousticPhrases();
            UpdatePhonemes();
        }

        async Task PlayPauseAsync(VoiSonaTalkEditorWordViewModel? startWord)
        {
            await playbackControlSemaphore.WaitAsync();
            try
            {
                if (IsPlaying)
                {
                    StopPlayback();
                    return;
                }

                IsBusy = true;
                SetWordPlaybackStatus(VoiSonaTalkEditorPlayButtonStatus.Busy);
                try
                {
                    await (Info?.VoiceItemEdit?.CreateVoiceFileAsync() ?? Task.CompletedTask);

                    stream = Info?.CreateItemAudioSource(new ItemAudioSourceCreationParameter(AudioEffectSelection.None) { RangeMode = ItemAudioSourceRangeMode.FullContentRange });
                    if (stream is null)
                        return;

                    stream.Seek(GetPlaybackStartTime(startWord));
                    player = new AudioPlayer(stream) { Volume = YMMSettings.Default.Volume / 100d };
                    player.StreamEnded += Player_StreamEnded;
                    player.Play();
                    IsPlaying = true;
                    SetWordPlaybackStatus(VoiSonaTalkEditorPlayButtonStatus.Playing);
                }
                finally
                {
                    IsBusy = false;
                    if (!IsPlaying)
                        SetWordPlaybackStatus(VoiSonaTalkEditorPlayButtonStatus.Idle);
                }
            }
            finally
            {
                playbackControlSemaphore.Release();
            }
        }

        private void Player_StreamEnded(object? sender, EventArgs e)
        {
            StopPlayback();
        }

        public void StopPlayback()
        {
            player?.StreamEnded -= Player_StreamEnded;
            player?.Dispose();
            player = null;
            stream?.Dispose();
            stream = null;
            IsPlaying = false;
            SetWordPlaybackStatus(VoiSonaTalkEditorPlayButtonStatus.Idle);
        }

        private void Pronounce_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(pronounce.TSML))
                UpdateAcousticPhrases();
            if (e.PropertyName == nameof(pronounce.Phonemes) || e.PropertyName == nameof(pronounce.PhonemeDurations))
                UpdatePhonemes();
        }

        void UpdatePhonemes()
        {
            var length = pronounce.Phonemes?.Length ?? 0;
            if (Phonemes.Count == length)
            {
                foreach (var vm in Phonemes)
                    vm.Refresh();
                return;
            }

            Phonemes = [.. Enumerable.Range(0, length).Select(i => new VoiSonaTalkEditorPhonemeViewModel(pronounce, i))];
        }

        void UpdateAcousticPhrases()
        {
            foreach(var phrase in AcousticPhrases)
                phrase.Changed -= Phrase_Changed;

            currentRoot = XDocument.Parse(pronounce.TSML ?? "<tsml></tsml>").Root;
            var acousticPhrases = currentRoot?.Elements("acoustic_phrase") ?? [];
            var wordIndex = 0;
            var phrases = acousticPhrases.Select((x, i) => new VoiSonaTalkEditorAcousticPhraseViewModel(x, i is 0, () => wordIndex++, playFromCommand)).ToImmutableList();
            foreach(var phrase in phrases)
                phrase.Changed += Phrase_Changed;
            AcousticPhrases = phrases;
        }

        TimeSpan GetPlaybackStartTime(VoiSonaTalkEditorWordViewModel? startWord)
        {
            if (startWord is null || currentRoot is null || pronounce.Phonemes is not { } phonemes || pronounce.PhonemeDurations is not { } durations)
                return TimeSpan.Zero;

            var startIndex = GetPhonemeStartIndex(startWord.WordIndex, phonemes);
            return TimeSpan.FromSeconds(durations.Take(startIndex).Sum());
        }

        int GetPhonemeStartIndex(int startWordIndex, string[] phonemes)
        {
            var cursor = 0;
            foreach (var (word, wordIndex) in (currentRoot?.Descendants("word") ?? []).Select((word, index) => (word, index)))
            {
                var wordPhonemes = GetWordPhonemes(word);
                var matchIndex = wordPhonemes.Length == 0 ? cursor : FindPhonemeSequence(phonemes, wordPhonemes, cursor);
                if (wordIndex == startWordIndex)
                    return Math.Clamp(matchIndex < 0 ? cursor : matchIndex, 0, phonemes.Length);

                if (matchIndex >= 0)
                    cursor = matchIndex + wordPhonemes.Length;
                else
                    cursor = Math.Min(cursor + wordPhonemes.Length, phonemes.Length);
            }
            return 0;
        }

        static string[] GetWordPhonemes(XElement word)
        {
            return ((string?)word.Attribute("phoneme") ?? string.Empty)
                .Split([',', '|', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        }

        static int FindPhonemeSequence(string[] phonemes, string[] target, int startIndex)
        {
            for (int i = startIndex; i <= phonemes.Length - target.Length; i++)
            {
                if (target.SequenceEqual(phonemes.Skip(i).Take(target.Length)))
                    return i;
            }
            return -1;
        }

        void SetWordPlaybackStatus(VoiSonaTalkEditorPlayButtonStatus status)
        {
            foreach (var word in AcousticPhrases.SelectMany(x => x.Words))
                word.SetStatus(status);
        }

        private void Phrase_Changed(object? sender, EventArgs e)
        {
            if (currentRoot != null)
                pronounce.TSML = currentRoot.ToString(SaveOptions.DisableFormatting);
        }
    }
}
