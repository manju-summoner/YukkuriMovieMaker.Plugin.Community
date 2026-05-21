using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;
using NAudio.Wave;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public enum RecordingDialogState
    {
        Idle,
        Recording,
        Recorded
    }

    public class RecordingWindowViewModel : Bindable, IDisposable
    {
        public RecordingWindowViewModel() : this(null, CreateDefaultArguments())
        {
        }

        private RecordPathService recordPathService;
        private RecordingService recordingService;
        private readonly VoiceTimelineInsertService voiceTimelineInsertService;
        private readonly TimelineSelectionService timelineSelectionService;
        private readonly RecordingScriptItemService recordingScriptItemService;
        private readonly RecordingScriptItem scriptItem;
        private readonly AudioPlaybackService audioPlaybackService;
        private RecordingStartWorkflowService recordingStartWorkflowService;
        private RecordingStopWorkflowService recordingStopWorkflowService;

        private string scriptText = string.Empty;
        private string status = Texts.Idle;
        private double currentVolume;
        private RecordingDialogState state = RecordingDialogState.Idle;
        private bool commandsReady;
        private string outputDirectory = string.Empty;
        private bool disposed;

        public RecordingWindowViewModel(string? initialText) : this(initialText, CreateDefaultArguments())
        {
        }

        private RecordingWindowViewModel(string? initialText, DefaultArguments arguments)
            : this(
                initialText,
                arguments.RecordPathService,
                arguments.RecordingService,
                arguments.VoiceTimelineInsertService,
                arguments.TimelineSelectionService,
                arguments.RecordingScriptItemService,
                arguments.ScriptItem,
                arguments.AudioPlaybackService,
                arguments.StartWorkflowService,
                arguments.StopWorkflowService)
        {
        }

        internal RecordingWindowViewModel(
            string? initialText,
            RecordPathService recordPathService,
            RecordingService recordingService,
            VoiceTimelineInsertService voiceTimelineInsertService,
            TimelineSelectionService timelineSelectionService,
            RecordingScriptItemService recordingScriptItemService,
            RecordingScriptItem scriptItem,
            AudioPlaybackService audioPlaybackService,
            RecordingStartWorkflowService recordingStartWorkflowService,
            RecordingStopWorkflowService recordingStopWorkflowService)
        {
            outputDirectory = RecordingSettings.Default.OutputDirectory;
            this.recordPathService = recordPathService;
            this.recordingService = recordingService;
            this.voiceTimelineInsertService = voiceTimelineInsertService;
            this.timelineSelectionService = timelineSelectionService;
            this.recordingScriptItemService = recordingScriptItemService;
            this.scriptItem = scriptItem;
            this.audioPlaybackService = audioPlaybackService;
            this.recordingStartWorkflowService = recordingStartWorkflowService;
            this.recordingStopWorkflowService = recordingStopWorkflowService;
            this.audioPlaybackService.PlaybackStopped += OnPlaybackStopped;

            StartRecordingCommand = CreateStartRecordingCommand();
            StopRecordingCommand = CreateStopRecordingCommand();
            AddToTimelineCommand = CreateAddToTimelineCommand();
            RegenerateCommand = CreateRegenerateCommand();
            PlayCommand = CreatePlayCommand();
            BrowseOutputDirectoryCommand = CreateBrowseOutputDirectoryCommand();
            ResetOutputDirectoryCommand = CreateResetOutputDirectoryCommand();
            ScriptText = ResolveInitialScriptText(initialText);

            RecordingLifecycleService.AttachRecordingEvents(this.recordingService, OnRecordingDataAvailable, OnRecordingStateChanged);
            EnsureSilentPlaceholderInCurrentDirectory();
            commandsReady = true;
            RaiseCommandStates();
        }

        private ActionCommand CreateStartRecordingCommand() => new(_ => CanStartRecording(), _ => StartRecording());
        private ActionCommand CreateStopRecordingCommand() => new(_ => CanStopRecording(), _ => StopRecording());
        private ActionCommand CreateAddToTimelineCommand() => new(_ => CanAddToTimeline(), _ => AddToTimeline());
        private ActionCommand CreateRegenerateCommand() => new(_ => CanRegenerate(), _ => Regenerate());
        private ActionCommand CreatePlayCommand() => new(_ => CanPlay(), _ => Play());
        private ActionCommand CreateBrowseOutputDirectoryCommand() => new(_ => RecordingDialogStateService.CanChangeOutputDirectory(State), _ => BrowseOutputDirectory());
        private ActionCommand CreateResetOutputDirectoryCommand() => new(_ => RecordingDialogStateService.CanChangeOutputDirectory(State), _ => ResetOutputDirectory());

        private string ResolveInitialScriptText(string? initialText)
        {
            if (!string.IsNullOrWhiteSpace(initialText))
                return initialText;

            return timelineSelectionService.TryGetSelectedSerif() ?? string.Empty;
        }

        public string ScriptText
        {
            get => scriptText;
            set
            {
                if (!Set(ref scriptText, value, nameof(ScriptText), nameof(DisplayText)))
                    return;
                if (commandsReady)
                    RaiseCommandStates();
            }
        }

        public string DisplayText => string.IsNullOrWhiteSpace(ScriptText) ? Texts.NoSerifSelected : ScriptText;

        public string OutputDirectory
        {
            get => outputDirectory;
            set
            {
                if (!RecordingDialogStateService.CanChangeOutputDirectory(State))
                {
                    Status = Texts.CannotChangeOutputWhileRecording;
                    return;
                }

                var normalized = string.IsNullOrWhiteSpace(value) ? RecordingSettings.GetDefaultOutputDirectory() : value.Trim();
                if (!Set(ref outputDirectory, normalized))
                    return;
                RecordingSettings.Default.OutputDirectory = outputDirectory;
                ReplaceRecordingService(outputDirectory);
                EnsureSilentPlaceholderInCurrentDirectory();
            }
        }

        public string Status
        {
            get => status;
            set => Set(ref status, value);
        }

        public double CurrentVolume
        {
            get => currentVolume;
            set => Set(ref currentVolume, value);
        }

        public RecordingDialogState State
        {
            get => state;
            private set
            {
                if (!Set(ref state, value))
                    return;
                RaiseCommandStates();
            }
        }

        public ActionCommand StartRecordingCommand { get; }
        public ActionCommand StopRecordingCommand { get; }
        public ActionCommand AddToTimelineCommand { get; }
        public ActionCommand RegenerateCommand { get; }
        public ActionCommand PlayCommand { get; }
        public ActionCommand BrowseOutputDirectoryCommand { get; }
        public ActionCommand ResetOutputDirectoryCommand { get; }

        private bool CanStartRecording() => RecordingDialogStateService.CanStartRecording(State, ScriptText);

        private bool CanStopRecording() => RecordingDialogStateService.CanStopRecording(State);

        private bool CanAddToTimeline() => RecordingDialogStateService.CanAddToTimeline(State);

        private bool CanRegenerate() => RecordingDialogStateService.CanRegenerate(State, ScriptText);

        private bool CanPlay()
        {
            var hasAudioFile = !string.IsNullOrWhiteSpace(scriptItem.AudioFilePath) && File.Exists(scriptItem.AudioFilePath);
            return RecordingDialogStateService.CanPlay(State, audioPlaybackService.IsPlaying, hasAudioFile);
        }

        private void StartRecording()
        {
            StopPlayback();

            if (string.IsNullOrWhiteSpace(ScriptText))
            {
                ScriptText = timelineSelectionService.TryGetSelectedSerif() ?? string.Empty;
            }

            try
            {
                var startResult = recordingStartWorkflowService.Execute(ScriptText);
                if (!startResult.IsSuccess)
                {
                    Status = startResult.ErrorMessage ?? Texts.SelectSerifInTimeline;
                    return;
                }

                State = RecordingDialogStateService.ToRecording();
                Status = Texts.RecordingNow;
            }
            catch (Exception ex)
            {
                Status = string.Format(Texts.RecordingStartFailed, ex.Message);
                State = RecordingDialogStateService.ToIdle();
            }
        }

        private async void StopRecording()
        {
            try
            {
                var result = await recordingStopWorkflowService.ExecuteAsync(scriptItem, ScriptText);
                CurrentVolume = 0;

                if (!result.HasData)
                {
                    TransitionToIdleWithStatus(Texts.NoRecordingDataPleaseRetry);
                    return;
                }

                ApplyStopRecordingResult(result);
            }
            catch (Exception ex)
            {
                TransitionToIdleWithStatus(string.Format(Texts.RecordingStopFailed, ex.Message));
            }
        }

        private void ApplyStopRecordingResult(Services.RecordingStopResult result)
        {
            State = RecordingDialogStateService.ToRecorded();
            var filePath = result.FilePath ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(result.NextSerif))
            {
                ScriptText = result.NextSerif;
                TransitionToIdleWithStatus(string.Format(Texts.RecordingCompletedNextPrepared, filePath));
                return;
            }

            Status = string.Format(Texts.RecordingCompletedAdded, filePath);
        }

        private void TransitionToIdleWithStatus(string message)
        {
            State = RecordingDialogStateService.ToIdle();
            Status = message;
        }

        private void Regenerate()
        {
            StopPlayback();
            EnsureSilentPlaceholderInCurrentDirectory();
            StartRecording();
        }

        private void BrowseOutputDirectory()
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = Texts.SelectOutputDirectory,
                UseDescriptionForTitle = true,
                SelectedPath = OutputDirectory
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                OutputDirectory = dialog.SelectedPath;
                Status = string.Format(Texts.OutputDirectoryChanged, OutputDirectory);
            }
        }

        private void ResetOutputDirectory()
        {
            OutputDirectory = RecordingSettings.GetDefaultOutputDirectory();
            Status = string.Format(Texts.OutputDirectoryReset, OutputDirectory);
        }

        private void Play()
        {
            if (string.IsNullOrWhiteSpace(scriptItem.AudioFilePath) || !File.Exists(scriptItem.AudioFilePath))
            {
                Status = Texts.NoPlayableAudio;
                return;
            }

            try
            {
                audioPlaybackService.Play(scriptItem.AudioFilePath);
                Status = Texts.Playing;
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                Status = string.Format(Texts.PlaybackFailed, ex.Message);
                audioPlaybackService.Stop();
                RaiseCommandStates();
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            void HandlePlaybackStopped()
            {
                if (e.Exception is not null)
                {
                    Status = string.Format(Texts.PlaybackError, e.Exception.Message);
                    RaiseCommandStates();
                    return;
                }

                Status = Texts.PlaybackCompleted;
                RaiseCommandStates();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                HandlePlaybackStopped();
            }
            else
            {
                _ = dispatcher.BeginInvoke((Action)HandlePlaybackStopped);
            }
        }

        private void StopPlayback(bool skipStopCall = false)
        {
            audioPlaybackService.Stop(skipStopCall);
            RaiseCommandStates();
        }

        private async void AddToTimeline()
        {
            try
            {
                await voiceTimelineInsertService.InsertAsync(scriptItem);
                Status = Texts.TimelineAdded;
            }
            catch (Exception ex)
            {
                Status = string.Format(Texts.TimelineAddFailed, ex.Message);
            }
        }

        private static TimeSpan GetAudioDuration(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new WaveFileReader(stream);
            return reader.TotalTime;
        }

        private void OnRecordingDataAvailable(object? sender, Models.RecordingDataEventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                CurrentVolume = e.Volume;
                return;
            }

            _ = dispatcher.BeginInvoke((Action)(() => CurrentVolume = e.Volume));
        }

        private void OnRecordingStateChanged(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                RaiseCommandStates();
                return;
            }

            _ = dispatcher.BeginInvoke((Action)RaiseCommandStates);
        }

        private void RaiseCommandStates()
        {
            if (!commandsReady)
                return;
            StartRecordingCommand?.RaiseCanExecuteChanged();
            StopRecordingCommand?.RaiseCanExecuteChanged();
            AddToTimelineCommand?.RaiseCanExecuteChanged();
            RegenerateCommand?.RaiseCanExecuteChanged();
            PlayCommand?.RaiseCanExecuteChanged();
            BrowseOutputDirectoryCommand?.RaiseCanExecuteChanged();
            ResetOutputDirectoryCommand?.RaiseCanExecuteChanged();
        }

        private void ReplaceRecordingService(string directory)
        {
            RecordingLifecycleService.DetachRecordingEvents(recordingService, OnRecordingDataAvailable, OnRecordingStateChanged);
            audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;
            RecordingLifecycleService.TryStopRecording(recordingService);

            recordPathService = new RecordPathService(directory);
            recordingService = new RecordingService(recordPathService);
            recordingStartWorkflowService = CreateRecordingStartWorkflowService(recordingService);
            recordingStopWorkflowService = CreateRecordingStopWorkflowService(recordingService);
            RecordingLifecycleService.AttachRecordingEvents(recordingService, OnRecordingDataAvailable, OnRecordingStateChanged);
            audioPlaybackService.PlaybackStopped += OnPlaybackStopped;
            RaiseCommandStates();
        }

        private static RecordingStartWorkflowService CreateRecordingStartWorkflowService(RecordingService service)
        {
            return new RecordingStartWorkflowService(service);
        }

        private RecordingStopWorkflowService CreateRecordingStopWorkflowService(RecordingService service)
        {
            return new RecordingStopWorkflowService(
                service,
                recordingScriptItemService,
                voiceTimelineInsertService,
                timelineSelectionService,
                GetAudioDuration);
        }

        private void EnsureSilentPlaceholderInCurrentDirectory()
        {
            if (scriptItem.IsRecorded)
                return;

            var silentPath = recordPathService.GetOrCreateSilentWavPath(TimeSpan.FromSeconds(5));
            if (string.IsNullOrWhiteSpace(silentPath))
                return;

            recordingScriptItemService.ApplySilentPlaceholder(
                scriptItem,
                silentPath,
                TimeSpan.FromSeconds(5),
                DateTime.Now);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            RecordingLifecycleService.DetachRecordingEvents(recordingService, OnRecordingDataAvailable, OnRecordingStateChanged);
            RecordingLifecycleService.TryStopRecording(recordingService);

            StopPlayback();
            audioPlaybackService.Dispose();
        }

        private static DefaultArguments CreateDefaultArguments()
        {
            var outputDirectory = RecordingSettings.Default.OutputDirectory;
            var recordPathService = new RecordPathService(outputDirectory);
            var recordingService = new RecordingService(recordPathService);
            var voiceTimelineInsertService = new VoiceTimelineInsertService();
            var timelineSelectionService = new TimelineSelectionService();
            var recordingScriptItemService = new RecordingScriptItemService();

            return new DefaultArguments
            {
                RecordPathService = recordPathService,
                RecordingService = recordingService,
                VoiceTimelineInsertService = voiceTimelineInsertService,
                TimelineSelectionService = timelineSelectionService,
                RecordingScriptItemService = recordingScriptItemService,
                ScriptItem = new RecordingScriptItem(),
                AudioPlaybackService = new AudioPlaybackService(),
                StartWorkflowService = new RecordingStartWorkflowService(recordingService),
                StopWorkflowService = new RecordingStopWorkflowService(
                    recordingService,
                    recordingScriptItemService,
                    voiceTimelineInsertService,
                    timelineSelectionService,
                    GetAudioDuration)
            };
        }

        private sealed class DefaultArguments
        {
            public required RecordPathService RecordPathService { get; init; }
            public required RecordingService RecordingService { get; init; }
            public required VoiceTimelineInsertService VoiceTimelineInsertService { get; init; }
            public required TimelineSelectionService TimelineSelectionService { get; init; }
            public required RecordingScriptItemService RecordingScriptItemService { get; init; }
            public required RecordingScriptItem ScriptItem { get; init; }
            public required AudioPlaybackService AudioPlaybackService { get; init; }
            public required RecordingStartWorkflowService StartWorkflowService { get; init; }
            public required RecordingStopWorkflowService StopWorkflowService { get; init; }
        }
    }
}





