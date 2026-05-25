using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS.EmojiPalette;

internal class IrodoriTTSEmojiPaletteViewModel : Bindable
{
    readonly IVoiceItemEditService? voiceItemEdit;
    readonly IEditorInfo? editorInfo;
    readonly SemaphoreSlim semaphore = new(1, 1);
    string? lastGeneratedHatsuon;

    IAudioStream? stream;
    AudioPlayer? player;

    public IReadOnlyList<EmojiItem> Emojis { get; } = IrodoriTTSEmojiDefinitions.All;

    public string Hatsuon
    {
        get => voiceItemEdit?.Hatsuon ?? string.Empty;
        set
        {
            if (voiceItemEdit != null)
                voiceItemEdit.Hatsuon = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy { get; set { if (Set(ref field, value)) UpdateCommands(); } }
    public bool IsPlaying { get; set => Set(ref field, value); }

    // View からキャレット位置を受け取るための Func
    public Func<int>? GetCaretIndex { get; set; }
    public Action<int>? SetCaretIndex { get; set; }

    public ICommand InsertEmojiCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand TogglePlayCommand { get; }

    public IrodoriTTSEmojiPaletteViewModel(IVoiceItemEditService? voiceItemEdit, IEditorInfo? editorInfo)
    {
        this.voiceItemEdit = voiceItemEdit;
        this.editorInfo = editorInfo;
        lastGeneratedHatsuon = voiceItemEdit?.Hatsuon;

        InsertEmojiCommand = new ActionCommand(
            _ => voiceItemEdit is not null,
            param =>
            {
                if (param is not EmojiItem emoji || voiceItemEdit is null)
                    return;

                var hatsuon = voiceItemEdit.Hatsuon ?? "";
                var caretIndex = GetCaretIndex?.Invoke() ?? hatsuon.Length;
                caretIndex = Math.Clamp(caretIndex, 0, hatsuon.Length);

                voiceItemEdit.Hatsuon = hatsuon.Insert(caretIndex, emoji.Emoji);
                OnPropertyChanged(nameof(Hatsuon));

                SetCaretIndex?.Invoke(caretIndex + emoji.Emoji.Length);
            });

        RegenerateCommand = new ActionCommand(
            _ => voiceItemEdit is not null && !IsBusy,
            async _ => await PlayAsync(forceRegenerate: true));

        TogglePlayCommand = new ActionCommand(
            _ => !IsBusy || IsPlaying,
            async _ =>
            {
                if (IsPlaying)
                    StopPlayback();
                else
                    await PlayAsync(forceRegenerate: false);
            });
    }

    void UpdateCommands()
    {
        (RegenerateCommand as ActionCommand)?.RaiseCanExecuteChanged();
        (TogglePlayCommand as ActionCommand)?.RaiseCanExecuteChanged();
    }

    async Task PlayAsync(bool forceRegenerate)
    {
        await semaphore.WaitAsync();
        try
        {
            StopPlayback();

            IsBusy = true;
            try
            {
                var currentHatsuon = voiceItemEdit?.Hatsuon;
                var needsRegenerate = forceRegenerate || currentHatsuon != lastGeneratedHatsuon;
                await (voiceItemEdit?.CreateVoiceFileAsync(force: needsRegenerate) ?? Task.CompletedTask);
                lastGeneratedHatsuon = currentHatsuon;
                voiceItemEdit?.IsHatsuonChanged = false;

                stream = editorInfo?.CreateItemAudioSource(new ItemAudioSourceCreationParameter(AudioEffectSelection.None) { RangeMode = ItemAudioSourceRangeMode.FullContentRange });
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
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS emoji palette playback failed", ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    void Player_StreamEnded(object? sender, EventArgs e)
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
    }
}
