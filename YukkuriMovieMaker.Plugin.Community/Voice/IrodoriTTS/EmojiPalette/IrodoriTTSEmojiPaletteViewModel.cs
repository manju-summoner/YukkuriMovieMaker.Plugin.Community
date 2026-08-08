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
    IVoiceItemEditService? voiceItemEdit;
    IEditorInfo? editorInfo;
    readonly SemaphoreSlim semaphore = new(1, 1);
    string? lastGeneratedHatsuon;
    string? lastNotifiedHatsuon;
    bool isClosed;
    // StopPlaybackのたびに進める世代番号（UIスレッド専用）。CreateVoiceFileAsyncのawait中に停止・差し替えが起きた場合、
    // 復帰後の世代照合で古い再生開始処理を打ち切るために使う
    int playbackGeneration;

    IAudioStream? stream;
    AudioPlayer? player;

    public IReadOnlyList<EmojiItem> Emojis { get; } = IrodoriTTSEmojiDefinitions.All;

    public string Hatsuon
    {
        get => voiceItemEdit?.Hatsuon ?? string.Empty;
        set
        {
            voiceItemEdit?.Hatsuon = value;
            lastNotifiedHatsuon = voiceItemEdit?.Hatsuon;
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
        lastNotifiedHatsuon = voiceItemEdit?.Hatsuon;

        InsertEmojiCommand = new ActionCommand(
            _ => this.voiceItemEdit is not null,
            param =>
            {
                if (param is not EmojiItem emoji || this.voiceItemEdit is not { } edit)
                    return;

                var hatsuon = edit.Hatsuon ?? "";
                var caretIndex = GetCaretIndex?.Invoke() ?? hatsuon.Length;
                caretIndex = Math.Clamp(caretIndex, 0, hatsuon.Length);

                edit.Hatsuon = hatsuon.Insert(caretIndex, emoji.Emoji);
                lastNotifiedHatsuon = edit.Hatsuon;
                OnPropertyChanged(nameof(Hatsuon));

                SetCaretIndex?.Invoke(caretIndex + emoji.Emoji.Length);
            });

        RegenerateCommand = new ActionCommand(
            _ => this.voiceItemEdit is not null && !IsBusy,
            async _ => await PlayAsync(forceRegenerate: true));

        TogglePlayCommand = new ActionCommand(
            _ => this.voiceItemEdit is not null && (!IsBusy || IsPlaying),
            async _ =>
            {
                if (IsPlaying)
                    StopPlayback();
                else
                    await PlayAsync(forceRegenerate: false);
            });
    }

    /// <summary>
    /// EditorInfoの更新を既存のViewModelに反映する。
    /// VMを作り直すとバインディング再評価やTextBoxのUndoスタックのリセットが起きるため、差し替えのみ行う。
    /// </summary>
    public void SetEditorInfo(IEditorInfo? info)
    {
        if (info is null)
            StopPlayback();

        var hadVoiceItemEdit = voiceItemEdit is not null;

        voiceItemEdit = info?.VoiceItemEdit;
        editorInfo = info;

        // ポップアップ表示外（EndEdit時など）に音声が再生成されていた場合に備え、
        // 発音の変更が確定済み（IsHatsuonChanged == false）なら生成済み発音の追跡値を現在値へ同期する
        if (voiceItemEdit is { IsHatsuonChanged: false } edit)
            lastGeneratedHatsuon = edit.Hatsuon;

        // EditorInfoはプレビュー再生中フレーム毎に更新されるため、値が変わったときだけ通知する。
        // VoiceItemEditはアクセス毎に同一アイテムへの新しいラッパーが返るため、
        // 直前のインスタンスから読んだ値ではなく「最後に通知した値」と比較する
        var newHatsuon = voiceItemEdit?.Hatsuon;
        if (lastNotifiedHatsuon != newHatsuon)
        {
            lastNotifiedHatsuon = newHatsuon;
            OnPropertyChanged(nameof(Hatsuon));
        }
        if (hadVoiceItemEdit != (voiceItemEdit is not null))
            UpdateCommands();
    }

    /// <summary>
    /// エディタから切り離されるときに呼ぶ。再生を停止し、進行中の再生開始処理も打ち切る。
    /// </summary>
    public void Close()
    {
        isClosed = true;
        StopPlayback();
    }

    void UpdateCommands()
    {
        // ActionCommandのRaiseCanExecuteChangedはCommandManager経由のグローバル通知のため実質1回で足りるが、
        // 対象コマンドの明示のため個別に呼んでいる
        (InsertEmojiCommand as ActionCommand)?.RaiseCanExecuteChanged();
        (RegenerateCommand as ActionCommand)?.RaiseCanExecuteChanged();
        (TogglePlayCommand as ActionCommand)?.RaiseCanExecuteChanged();
    }

    async Task PlayAsync(bool forceRegenerate)
    {
        await semaphore.WaitAsync();
        try
        {
            if (isClosed)
                return;

            StopPlayback();
            var generation = playbackGeneration;

            IsBusy = true;
            try
            {
                // await中にSetEditorInfoで参照が差し替わっても、同一アイテムの組で処理を続けるためローカルに捕捉する
                var edit = voiceItemEdit;
                var info = editorInfo;
                var currentHatsuon = edit?.Hatsuon;
                // 発音テキストの編集だけではforce:falseの再生成条件を満たさない場合があるため、
                // IsHatsuonChangedが立っているときはテキストが一致していても再生成する
                var needsRegenerate = forceRegenerate || currentHatsuon != lastGeneratedHatsuon || edit?.IsHatsuonChanged is true;
                await (edit?.CreateVoiceFileAsync(force: needsRegenerate) ?? Task.CompletedTask);
                // 生成処理が例外なく完了した場合は、この後停止・差し替えで再生を打ち切るときでも生成済み状態を確定させる。
                // ただしawait中に発音が編集されていた場合、その変更はまだ音声に反映されていないためフラグを維持する
                if (edit is not null && edit.Hatsuon == currentHatsuon)
                {
                    edit.IsHatsuonChanged = false;
                    // アイテム差し替え後はSetEditorInfoが同期した新アイテムの値を上書きしない
                    if (voiceItemEdit?.Hatsuon == currentHatsuon)
                        lastGeneratedHatsuon = currentHatsuon;
                }
                if (isClosed || generation != playbackGeneration)
                    return;

                stream = info?.CreateItemAudioSource(new ItemAudioSourceCreationParameter(AudioEffectSelection.None) { RangeMode = ItemAudioSourceRangeMode.FullContentRange });
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
        playbackGeneration++;
        player?.StreamEnded -= Player_StreamEnded;
        player?.Dispose();
        player = null;
        stream?.Dispose();
        stream = null;
        IsPlaying = false;
    }
}
