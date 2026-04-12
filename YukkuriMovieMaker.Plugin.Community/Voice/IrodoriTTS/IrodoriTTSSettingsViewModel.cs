using NAudio.Wave;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSSettingsViewModel : Bindable
{
    // 参照音声一覧
    public ObservableCollection<IrodoriTTSRefVoiceItem> RefVoiceItems { get; } = [];
    public IrodoriTTSRefVoiceItem? SelectedRefVoice
    {
        get => field;
        set
        {
            if (Set(ref field, value))
                UpdateRefVoiceCommands();
        }
    }
    public bool IsPlaying { get => field; set => Set(ref field, value); }

    // サーバーステータス
    public string ServerStatusText { get => field; set => Set(ref field, value); } = Texts.ServerStatusUnknown;

    // コマンド
    public ICommand CreateNewCommand { get; }
    public ICommand DeleteRefVoiceCommand { get; }
    public ICommand PreviewRefVoiceCommand { get; }
    public ICommand CheckServerStatusCommand { get; }
    public ICommand ShutdownServerCommand { get; }

    AudioFileReader? audioFileReader;
    AudioPlayer? audioPlayer;
    RefVoiceFile? previewRefVoice;

    public IrodoriTTSSettingsViewModel()
    {
        CreateNewCommand = new ActionCommand(
            _ => true,
            _ => OpenVoiceDesignDialog());

        DeleteRefVoiceCommand = new ActionCommand(
            _ => SelectedRefVoice != null,
            _ => DeleteSelectedRefVoice());

        PreviewRefVoiceCommand = new ActionCommand(
            _ => SelectedRefVoice != null,
            _ => TogglePreviewRefVoice());

        CheckServerStatusCommand = new ActionCommand(
            _ => true,
            _ => CheckServerStatus());

        ShutdownServerCommand = new ActionCommand(
            _ => true,
            _ => ShutdownServer());

        LoadRefVoiceList();
    }

    public void LoadRefVoiceList()
    {
        RefVoiceItems.Clear();
        var dir = RefVoiceFile.RefVoicesDirectory;
        if (!Directory.Exists(dir))
            return;

        foreach (var file in Directory.EnumerateFiles(dir, "*.ymm4refvoice"))
        {
            try
            {
                var meta = RefVoiceFile.FastLoad(file);
                RefVoiceItems.Add(new IrodoriTTSRefVoiceItem
                {
                    FilePath = file,
                    Name = meta.Name,
                    Caption = meta.Caption,
                    SourceApplication = meta.SourceApplication,
                });
            }
            catch (Exception ex)
            {
                // 壊れたファイルはスキップ
                Log.Default.Write($"Irodori-TTS failed to load ref voice: {file}", ex);
            }
        }
    }

    void OpenVoiceDesignDialog()
    {
        var dialog = new IrodoriTTSVoiceDesignDialog
        {
            Owner = Application.Current.MainWindow,
        };

        if (dialog.ShowDialog() == true)
        {
            // 保存された場合、一覧を再読み込み
            LoadRefVoiceList();
        }
    }

    void TogglePreviewRefVoice()
    {
        if (IsPlaying)
        {
            StopPlayback();
            return;
        }

        if (SelectedRefVoice == null)
            return;

        try
        {
            previewRefVoice = RefVoiceFile.Load(SelectedRefVoice.FilePath);
            if (!previewRefVoice.HasRefFilePath)
            {
                previewRefVoice.Dispose();
                previewRefVoice = null;
                return;
            }

            audioFileReader = new AudioFileReader(previewRefVoice.RefFilePath!);
            audioPlayer = new AudioPlayer(audioFileReader) { Volume = YMMSettings.Default.Volume / 100d };
            audioPlayer.StreamEnded += Player_StreamEnded;
            audioPlayer.Play();
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS failed to preview ref voice", ex);
            StopPlayback();
        }
    }

    void Player_StreamEnded(object? sender, EventArgs e)
    {
        StopPlayback();
    }

    void StopPlayback()
    {
        audioPlayer?.StreamEnded -= Player_StreamEnded;
        audioPlayer?.Dispose();
        audioPlayer = null;
        audioFileReader?.Dispose();
        audioFileReader = null;
        previewRefVoice?.Dispose();
        previewRefVoice = null;
        IsPlaying = false;
    }

    void UpdateRefVoiceCommands()
    {
        (DeleteRefVoiceCommand as ActionCommand)?.RaiseCanExecuteChanged();
        (PreviewRefVoiceCommand as ActionCommand)?.RaiseCanExecuteChanged();
    }

    void CheckServerStatus()
    {
        if (IrodoriTTSGradioServer.IsRunning)
        {
            var appName = IrodoriTTSGradioServer.CurrentAppName ?? "???";
            ServerStatusText = string.Format(Texts.ServerStatusRunning, appName);
        }
        else
        {
            ServerStatusText = Texts.ServerStatusStopped;
        }
    }

    void ShutdownServer()
    {
        IrodoriTTSGradioServer.Shutdown();
        ServerStatusText = Texts.ServerStatusStopped;
    }

    void DeleteSelectedRefVoice()
    {
        if (SelectedRefVoice == null)
            return;

        var result = MessageBox.Show(
            Texts.DeleteConfirmMessage,
            Texts.Delete,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        StopPlayback();

        try
        {
            if (File.Exists(SelectedRefVoice.FilePath))
                File.Delete(SelectedRefVoice.FilePath);
            RefVoiceItems.Remove(SelectedRefVoice);
        }
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS failed to delete ref voice", ex);
        }
    }
}
