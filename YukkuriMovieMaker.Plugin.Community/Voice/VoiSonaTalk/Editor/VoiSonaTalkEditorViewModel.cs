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

        ImmutableList<VoiSonaTalkEditorAcousticPhraseViewModel> acousticPhrases = [];
        public ImmutableList<VoiSonaTalkEditorAcousticPhraseViewModel> AcousticPhrases { get => acousticPhrases; set => Set(ref acousticPhrases, value); }

        public bool IsPlaying { get => isPlaying; set => Set(ref isPlaying, value); }
        public bool IsBusy { get => isBusy; set => Set(ref isBusy, value); }
        public ICommand TogglePlayCommand { get; }
        public IEditorInfo? Info { get; set; }

        public VoiSonaTalkEditorViewModel(VoiSonaTalkVoicePronounce pronounce)
        {
            this.pronounce = pronounce;
            WeakEventManager<VoiSonaTalkVoicePronounce, PropertyChangedEventArgs>.AddHandler(pronounce, nameof(pronounce.PropertyChanged), Pronounce_PropertyChanged);
            UpdateAcousticPhrases();

            TogglePlayCommand = new ActionCommand(
                _=> true,
                async _ =>
                {
                    await playbackControlSemaphore.WaitAsync();
                    try
                    {
                        if (IsPlaying)
                        {
                            StopPlayback();
                        }
                        else
                        {
                            IsBusy = true;
                            try
                            {
                                await (Info?.VoiceItemEdit?.CreateVoiceFileAsync() ?? Task.CompletedTask);

                                stream = Info?.CreateItemAudioSource(AudioEffectSelection.None);
                                if (stream is null)
                                    return;
                                player = new AudioPlayer(stream) { Volume = YMMSettings.Default.Volume / 100d };
                                player.StreamEnded += Player_StreamEnded;
                                player.Play();
                                IsPlaying = true;
                            }
                            finally
                            {
                                IsBusy = false;
                            }

                        }
                    }
                    finally
                    {
                        playbackControlSemaphore.Release();
                    }
                });
        }

        private void Player_StreamEnded(object? sender, EventArgs e)
        {
            StopPlayback();
        }

        public void StopPlayback()
        {
            if(player != null)
                player.StreamEnded -= Player_StreamEnded;
            player?.Dispose();
            player = null;
            stream?.Dispose();
            stream = null;
            IsPlaying = false;
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
