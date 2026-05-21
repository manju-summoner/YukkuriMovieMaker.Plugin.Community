using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public class ToolViewModel : Bindable, ITimelineToolViewModel
    {
        private readonly RecordingService recordingService;
        private readonly ToolRecordingStartWorkflowService recordingStartWorkflowService;
        private readonly ToolRecordingStopWorkflowService recordingStopWorkflowService;
        private readonly TimelineInsertService timelineInsertService;
        private readonly AudioPlaybackService audioPlaybackService;
        private readonly TimelineSelectionService timelineSelectionService;
        private RecordingWindow? recordingWindow;
        private string? latestRecordedFilePath;
        private string? lastTimelineAddedFilePath;

        public ObservableCollection<string> AvailableDevices { get; } = new();

        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand OpenRecordingWindowCommand { get; }
        public ICommand BrowseOutputDirectoryCommand { get; }
        public ICommand ResetOutputDirectoryCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand AddToTimelineCommand { get; }

        private string? selectedDevice;
        public string? SelectedDevice
        {
            get => selectedDevice;
            set
            {
                if (Set(ref selectedDevice, value))
                    RaiseCommandStates();
            }
        }

        private string recordingStatus = Texts.Idle;
        public string RecordingStatus
        {
            get => recordingStatus;
            set => Set(ref recordingStatus, value);
        }

        private double currentVolume;
        public double CurrentVolume
        {
            get => currentVolume;
            set => Set(ref currentVolume, value);
        }

        private string recordsDirectory = string.Empty;
        public string RecordsDirectory
        {
            get => recordsDirectory;
            set => Set(ref recordsDirectory, value);
        }

        public string OutputDirectory
        {
            get => recordsDirectory;
            set
            {
                if (IsRecording)
                {
                    RecordingStatus = Texts.CannotChangeOutputWhileRecording;
                    return;
                }

                var normalized = string.IsNullOrWhiteSpace(value)
                    ? RecordingSettings.GetDefaultOutputDirectory()
                    : value.Trim();

                if (!Set(ref recordsDirectory, normalized, nameof(RecordsDirectory), nameof(OutputDirectory)))
                    return;

                RecordingSettings.Default.OutputDirectory = normalized;
            }
        }

        public bool IsRecording => recordingService.IsRecording;

        public static Timeline? TimelineInstance { get; private set; }
        public Timeline Timeline { get; set; } = null!;

        public ToolViewModel()
            : this(CreateDefaultArguments())
        {
        }

        private ToolViewModel(DefaultArguments arguments)
            : this(
                arguments.RecordingService,
                arguments.StartWorkflowService,
                arguments.StopWorkflowService,
                arguments.TimelineInsertService,
                arguments.AudioPlaybackService,
                arguments.RecordPathService,
                arguments.TimelineSelectionService)
        {
        }

        internal ToolViewModel(
            RecordingService recordingService,
            ToolRecordingStartWorkflowService recordingStartWorkflowService,
            ToolRecordingStopWorkflowService recordingStopWorkflowService,
            TimelineInsertService timelineInsertService,
            AudioPlaybackService audioPlaybackService,
            RecordPathService recordPathService,
            TimelineSelectionService timelineSelectionService)
        {
            this.recordingService = recordingService;
            this.recordingStartWorkflowService = recordingStartWorkflowService;
            this.recordingStopWorkflowService = recordingStopWorkflowService;
            this.timelineInsertService = timelineInsertService;
            this.audioPlaybackService = audioPlaybackService;
            this.timelineSelectionService = timelineSelectionService;

            recordingService.DataAvailable += OnRecordingDataAvailable;
            recordingService.RecordingStateChanged += OnRecordingStateChanged;
            this.audioPlaybackService.PlaybackStopped += OnPlaybackStopped;

            StartRecordingCommand = new ActionCommand(_ => CanStartRecording(), _ => StartRecording());
            StopRecordingCommand = new ActionCommand(_ => CanStopRecording(), _ => StopRecording());
            RefreshDevicesCommand = new ActionCommand(_ => true, _ => RefreshMicrophones());
            OpenRecordingWindowCommand = new ActionCommand(_ => CanOpenRecordingWindow(), _ => OpenRecordingWindow());
            BrowseOutputDirectoryCommand = new ActionCommand(_ => !IsRecording, _ => BrowseOutputDirectory());
            ResetOutputDirectoryCommand = new ActionCommand(_ => !IsRecording, _ => ResetOutputDirectory());
            PlayCommand = new ActionCommand(_ => CanPlay(), _ => Play());
            AddToTimelineCommand = new ActionCommand(_ => CanAddToTimeline(), _ => AddToTimeline());

            RecordsDirectory = recordPathService.GetRecordsDirectory();
            RefreshMicrophones();
        }

        private static RecordingService CreateDefaultRecordingService(out RecordPathService recordPathService)
        {
            recordPathService = new RecordPathService();
            return new RecordingService(recordPathService);
        }

        private static DefaultArguments CreateDefaultArguments()
        {
            var recordingService = CreateDefaultRecordingService(out var recordPathService);
            return new DefaultArguments
            {
                RecordingService = recordingService,
                StartWorkflowService = CreateDefaultStartWorkflowService(recordingService),
                StopWorkflowService = CreateDefaultStopWorkflowService(recordingService),
                TimelineInsertService = new TimelineInsertService(),
                AudioPlaybackService = new AudioPlaybackService(),
                RecordPathService = recordPathService,
                TimelineSelectionService = CreateDefaultTimelineSelectionService()
            };
        }

        private static ToolRecordingStartWorkflowService CreateDefaultStartWorkflowService(RecordingService recordingService)
        {
            return new ToolRecordingStartWorkflowService(recordingService);
        }

        private static ToolRecordingStopWorkflowService CreateDefaultStopWorkflowService(RecordingService recordingService)
        {
            return new ToolRecordingStopWorkflowService(recordingService);
        }

        private static TimelineSelectionService CreateDefaultTimelineSelectionService()
        {
            return new TimelineSelectionService();
        }

        private sealed class DefaultArguments
        {
            public required RecordingService RecordingService { get; init; }
            public required ToolRecordingStartWorkflowService StartWorkflowService { get; init; }
            public required ToolRecordingStopWorkflowService StopWorkflowService { get; init; }
            public required TimelineInsertService TimelineInsertService { get; init; }
            public required AudioPlaybackService AudioPlaybackService { get; init; }
            public required RecordPathService RecordPathService { get; init; }
            public required TimelineSelectionService TimelineSelectionService { get; init; }
        }

        public void RefreshMicrophones()
        {
            AvailableDevices.Clear();

            foreach (var deviceName in recordingService.GetAvailableDeviceNames())
            {
                AvailableDevices.Add(deviceName);
            }

            if (AvailableDevices.Count > 0)
            {
                if (SelectedDevice is null || !AvailableDevices.Contains(SelectedDevice))
                    SelectedDevice = AvailableDevices[0];
            }
            else
            {
                SelectedDevice = null;
                RecordingStatus = Texts.NoRecordingDeviceFound;
            }

            RaiseCommandStates();
        }

        private bool CanStartRecording() => !IsRecording && !string.IsNullOrWhiteSpace(SelectedDevice);

        private bool CanStopRecording() => IsRecording;

        private bool CanOpenRecordingWindow() => !IsRecording;
        private bool CanPlay() => !IsRecording && !audioPlaybackService.IsPlaying && HasPlayableAudioFile();
        private bool CanAddToTimeline()
            => !IsRecording
                && !audioPlaybackService.IsPlaying
                && HasPlayableAudioFile()
                && !string.Equals(latestRecordedFilePath, lastTimelineAddedFilePath, StringComparison.OrdinalIgnoreCase);

        private void StartRecording()
        {
            try
            {
                if (audioPlaybackService.IsPlaying)
                    audioPlaybackService.Stop();

                var startResult = recordingStartWorkflowService.Execute(SelectedDevice);
                if (!startResult.IsSuccess)
                {
                    RecordingStatus = startResult.ErrorMessage ?? Texts.ReselectRecordingDevice;
                    return;
                }

                latestRecordedFilePath = null;
                lastTimelineAddedFilePath = null;
                RecordingStatus = string.Format(Texts.RecordingNowWithPath, RecordsDirectory);
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordingStartFailed, ex.Message);
                RaiseCommandStates();
            }
        }

        private async void StopRecording()
        {
            try
            {
                var stopResult = await recordingStopWorkflowService.ExecuteAsync();

                CurrentVolume = 0;
                RaiseCommandStates();

                if (!stopResult.HasData)
                {
                    RecordingStatus = Texts.NoRecordingDataRetry;
                    return;
                }

                if (stopResult.HasZeroLengthData)
                {
                    RecordingStatus = string.Format(Texts.RecordingDataLengthZero, stopResult.FilePath ?? string.Empty);
                    return;
                }

                latestRecordedFilePath = stopResult.FilePath;
                lastTimelineAddedFilePath = null;
                RecordingStatus = string.Format(Texts.RecordingStoppedAndSaved, stopResult.FilePath ?? string.Empty);
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordingStopFailed, ex.Message);
            }
        }

        private void Play()
        {
            if (!HasPlayableAudioFile())
            {
                RecordingStatus = Texts.NoPlayableAudio;
                RaiseCommandStates();
                return;
            }

            try
            {
                audioPlaybackService.Play(latestRecordedFilePath!);
                RecordingStatus = Texts.Playing;
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.PlaybackFailed, ex.Message);
                audioPlaybackService.Stop();
            }
            finally
            {
                RaiseCommandStates();
            }
        }

        private async void AddToTimeline()
        {
            if (!HasPlayableAudioFile())
            {
                RecordingStatus = Texts.NoRecordingDataRetry;
                RaiseCommandStates();
                return;
            }

            try
            {
                var filePath = latestRecordedFilePath!;
                await timelineInsertService.InsertAsync(new Models.RecordedFileInfo
                {
                    FilePath = filePath
                });

                lastTimelineAddedFilePath = filePath;
                RecordingStatus = Texts.TimelineAdded;
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.TimelineAddFailed, ex.Message);
            }
            finally
            {
                RaiseCommandStates();
            }
        }

        private void OpenRecordingWindow()
        {
            if (recordingWindow is not null)
            {
                if (recordingWindow.WindowState == WindowState.Minimized)
                    recordingWindow.WindowState = WindowState.Normal;
                recordingWindow.Activate();
                return;
            }

            var serif = timelineSelectionService.TryGetSelectedSerif();

            recordingWindow = new RecordingWindow(serif, SelectedDevice)
            {
                Owner = null,
                ShowInTaskbar = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true
            };
            recordingWindow.Closed += (_, _) =>
            {
                recordingWindow = null;
            };
            recordingWindow.Show();
            recordingWindow.Activate();
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
                RecordingStatus = string.Format(Texts.OutputDirectoryChanged, OutputDirectory);
            }
        }

        private void ResetOutputDirectory()
        {
            OutputDirectory = RecordingSettings.GetDefaultOutputDirectory();
            RecordingStatus = string.Format(Texts.OutputDirectoryReset, OutputDirectory);
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
                OnPropertyChanged(nameof(IsRecording));
                RaiseCommandStates();
                return;
            }

            _ = dispatcher.BeginInvoke((Action)(() =>
            {
                OnPropertyChanged(nameof(IsRecording));
                RaiseCommandStates();
            }));
        }

        private void OnPlaybackStopped(object? sender, NAudio.Wave.StoppedEventArgs e)
        {
            void HandlePlaybackStopped()
            {
                if (e.Exception is not null)
                {
                    RecordingStatus = string.Format(Texts.PlaybackError, e.Exception.Message);
                }
                else
                {
                    RecordingStatus = Texts.PlaybackCompleted;
                }

                RaiseCommandStates();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                HandlePlaybackStopped();
                return;
            }

            _ = dispatcher.BeginInvoke((Action)HandlePlaybackStopped);
        }

        public void SetTimelineToolInfo(TimelineToolInfo info)
        {
            TimelineInstance = info.Timeline;
        }

        private void RaiseCommandStates()
        {
            if (StartRecordingCommand is ActionCommand start)
                start.RaiseCanExecuteChanged();

            if (StopRecordingCommand is ActionCommand stop)
                stop.RaiseCanExecuteChanged();

            if (OpenRecordingWindowCommand is ActionCommand open)
                open.RaiseCanExecuteChanged();

            if (BrowseOutputDirectoryCommand is ActionCommand browse)
                browse.RaiseCanExecuteChanged();

            if (ResetOutputDirectoryCommand is ActionCommand reset)
                reset.RaiseCanExecuteChanged();

            if (PlayCommand is ActionCommand play)
                play.RaiseCanExecuteChanged();

            if (AddToTimelineCommand is ActionCommand addToTimeline)
                addToTimeline.RaiseCanExecuteChanged();
        }

        private bool HasPlayableAudioFile()
        {
            return !string.IsNullOrWhiteSpace(latestRecordedFilePath) && File.Exists(latestRecordedFilePath);
        }

    }
}



