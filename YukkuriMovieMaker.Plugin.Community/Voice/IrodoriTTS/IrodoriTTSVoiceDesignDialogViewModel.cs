using NAudio.Wave;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Settings;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS;

internal class IrodoriTTSVoiceDesignDialogViewModel : Bindable
{
    // 入力
    public string SpeakerName { get => field; set { if (Set(ref field, value)) UpdateCommands(); } } = string.Empty;
    public string Caption { get => field; set { if (Set(ref field, value)) UpdateCommands(); } } = string.Empty;
    public string SpeechText { get => field; set { if (Set(ref field, value)) UpdateCommands(); } } = string.Empty;
    public string Seed { get => field; set => Set(ref field, value); } = string.Empty;
    public double NumSteps { get => field; set => Set(ref field, value); } = 40;
    public string Checkpoint { get => field; set => Set(ref field, value); } = string.Empty;

    // 状態
    public string ErrorText { get => field; set => Set(ref field, value); } = string.Empty;
    public bool IsGenerating { get => field; set { if (Set(ref field, value)) UpdateCommands(); } }
    public bool HasGeneratedAudio { get => field; set { if (Set(ref field, value)) UpdateCommands(); } }
    public bool IsPlaying { get => field; set => Set(ref field, value); }
    public bool IsSaving { get => field; set { if (Set(ref field, value)) UpdateCommands(); } }
    public bool IsSaved { get => field; private set => Set(ref field, value); }

    // コマンド
    public ICommand GenerateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand SaveCommand { get; }

    string? generatedAudioPath;
    string generatedSpeechText = string.Empty;
    string generatedCaption = string.Empty;
    string generatedSeed = string.Empty;
    int generatedNumSteps;
    string generatedCheckpoint = string.Empty;
    AudioFileReader? audioFileReader;
    AudioPlayer? audioPlayer;

    public IrodoriTTSVoiceDesignDialogViewModel()
    {
        GenerateCommand = new ActionCommand(
            _ => !IsGenerating,
            async _ => await GenerateAsync());

        PreviewCommand = new ActionCommand(
            _ => HasGeneratedAudio,
            _ => TogglePreview());

        SaveCommand = new ActionCommand(
            _ => HasGeneratedAudio && !IsSaving,
            _ => Save());

        // 前回の設定を復元（話者名以外）
        var settings = IrodoriTTSSettings.Default;
        Caption = settings.LastCaption;
        SpeechText = settings.LastSpeechText;
        Seed = settings.LastSeed;
        NumSteps = settings.LastNumSteps;
        Checkpoint = settings.LastVoiceDesignCheckpoint;
    }

    async Task GenerateAsync()
    {
        StopPlayback();
        IsGenerating = true;
        ErrorText = string.Empty;
        HasGeneratedAudio = false;

        try
        {
            var url = await IrodoriTTSGradioServer.EnsureVoiceDesignServerAsync();

            var tempDir = AppDirectories.TemporaryDirectory;
            Directory.CreateDirectory(tempDir);
            generatedAudioPath = Path.Combine(tempDir, $"voicedesign_{Guid.NewGuid()}.wav");

            generatedSpeechText = string.IsNullOrWhiteSpace(SpeechText) ? Texts.SpeechTextPlaceholder : SpeechText;
            generatedCaption = string.IsNullOrEmpty(Caption) ? Texts.CaptionPlaceholder : Caption;
            generatedSeed = Seed;
            generatedNumSteps = (int)NumSteps;
            generatedCheckpoint = Checkpoint;
            await IrodoriTTSAPI.VoiceDesignAsync(url, generatedSpeechText, generatedCaption, generatedSeed, generatedNumSteps, generatedAudioPath, generatedCheckpoint);

            HasGeneratedAudio = true;
            ErrorText = string.Empty;
            TogglePreview();
        }
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS VoiceDesign generation failed", ex);
            ErrorText = $"{Texts.FailedToGenerate}: {ex.Message}";
            HasGeneratedAudio = false;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    void TogglePreview()
    {
        if (IsPlaying)
        {
            StopPlayback();
            return;
        }

        if (generatedAudioPath == null || !File.Exists(generatedAudioPath))
            return;

        try
        {
            audioFileReader = new AudioFileReader(generatedAudioPath);
            audioPlayer = new AudioPlayer(audioFileReader) { Volume = YMMSettings.Default.Volume / 100d };
            audioPlayer.StreamEnded += Player_StreamEnded;
            audioPlayer.Play();
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS VoiceDesign preview failed", ex);
            StopPlayback();
        }
    }

    void Player_StreamEnded(object? sender, EventArgs e)
    {
        StopPlayback();
    }

    void StopPlayback()
    {
        if (audioPlayer != null)
            audioPlayer.StreamEnded -= Player_StreamEnded;
        audioPlayer?.Dispose();
        audioPlayer = null;
        audioFileReader?.Dispose();
        audioFileReader = null;
        IsPlaying = false;
    }


    void UpdateCommands()
    {
        (GenerateCommand as ActionCommand)?.RaiseCanExecuteChanged();
        (PreviewCommand as ActionCommand)?.RaiseCanExecuteChanged();
        (SaveCommand as ActionCommand)?.RaiseCanExecuteChanged();
    }

    void Save()
    {
        if (generatedAudioPath == null || !File.Exists(generatedAudioPath))
            return;

        var speakerName = string.IsNullOrWhiteSpace(SpeakerName) ? Texts.SpeakerNamePlaceholder : SpeakerName;

        IsSaving = true;
        try
        {
            var refVoice = new RefVoiceFile
            {
                Name = speakerName,
                Text = generatedSpeechText,
                Caption = generatedCaption,
                SourceApplication = "Irodori-TTS-VoiceDesign",
                GenerationSettings = new
                {
                    seed = generatedSeed,
                    numSteps = generatedNumSteps,
                    checkpoint = generatedCheckpoint,
                },
            };
            refVoice.SetAudioFromFile(generatedAudioPath);

            var dir = RefVoiceFile.RefVoicesDirectory;
            Directory.CreateDirectory(dir);

            var safeFileName = string.Join("_", speakerName.Split(Path.GetInvalidFileNameChars()));
            var savePath = Path.Combine(dir, $"{safeFileName}.ymm4refvoice");

            if (File.Exists(savePath))
            {
                var result = MessageBox.Show(
                    string.Format(Texts.OverwriteConfirmMessage, speakerName),
                    Texts.Save,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }
            }

            refVoice.Save(savePath);

            // 話者名以外の設定を保存
            var settings = IrodoriTTSSettings.Default;
            settings.LastCaption = Caption;
            settings.LastSpeechText = SpeechText;
            settings.LastSeed = Seed;
            settings.LastNumSteps = NumSteps;
            settings.LastVoiceDesignCheckpoint = Checkpoint;

            IsSaved = true;
            ErrorText = string.Empty;
        }
        catch (Exception ex)
        {
            Log.Default.Write("Irodori-TTS VoiceDesign save failed", ex);
            ErrorText = ex.Message;
            IsSaving = false;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
