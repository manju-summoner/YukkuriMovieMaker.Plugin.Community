using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
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
        private readonly TimelineSelectionService timelineSelectionService;
        private RecordingWindow? recordingWindow;

        public ObservableCollection<string> AvailableDevices { get; } = new();

        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand OpenRecordingWindowCommand { get; }

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

        private string recordingStatus = "待機中";
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
                arguments.RecordPathService,
                arguments.TimelineSelectionService)
        {
        }

        internal ToolViewModel(
            RecordingService recordingService,
            ToolRecordingStartWorkflowService recordingStartWorkflowService,
            ToolRecordingStopWorkflowService recordingStopWorkflowService,
            RecordPathService recordPathService,
            TimelineSelectionService timelineSelectionService)
        {
            this.recordingService = recordingService;
            this.recordingStartWorkflowService = recordingStartWorkflowService;
            this.recordingStopWorkflowService = recordingStopWorkflowService;
            this.timelineSelectionService = timelineSelectionService;

            recordingService.DataAvailable += OnRecordingDataAvailable;
            recordingService.RecordingStateChanged += OnRecordingStateChanged;

            StartRecordingCommand = new ActionCommand(_ => CanStartRecording(), _ => StartRecording());
            StopRecordingCommand = new ActionCommand(_ => CanStopRecording(), _ => StopRecording());
            RefreshDevicesCommand = new ActionCommand(_ => true, _ => RefreshMicrophones());
            OpenRecordingWindowCommand = new ActionCommand(_ => CanOpenRecordingWindow(), _ => OpenRecordingWindow());

            RecordsDirectory = recordPathService.GetRecordsDirectory();
            RefreshMicrophones();
        }

        private static RecordingService CreateDefaultRecordingService(out RecordPathService recordPathService)
        {
            recordPathService = new RecordPathService(RecordingSettings.Default.OutputDirectory);
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
            return new ToolRecordingStopWorkflowService(recordingService, new TimelineInsertService());
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
                RecordingStatus = "録音デバイスが見つかりません。";
            }

            RaiseCommandStates();
        }

        private bool CanStartRecording() => !IsRecording && !string.IsNullOrWhiteSpace(SelectedDevice);

        private bool CanStopRecording() => IsRecording;

        private bool CanOpenRecordingWindow() => !IsRecording;

        private void StartRecording()
        {
            try
            {
                var startResult = recordingStartWorkflowService.Execute(SelectedDevice);
                if (!startResult.IsSuccess)
                {
                    RecordingStatus = startResult.ErrorMessage ?? "録音デバイスを再選択してください。";
                    return;
                }

                RecordingStatus = $"録音中... 保存先: {RecordsDirectory}";
            }
            catch (Exception ex)
            {
                RecordingStatus = $"録音開始に失敗しました: {ex.Message}";
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
                    RecordingStatus = "録音データがありません。再録音してください。";
                    return;
                }

                if (stopResult.HasZeroLengthData)
                {
                    RecordingStatus = $"録音データ長が 0 のため追加しませんでした: {stopResult.FilePath ?? string.Empty}";
                    return;
                }

                RecordingStatus = $"録音停止。タイムラインへ追加しました: {stopResult.FilePath ?? string.Empty}";
            }
            catch (Exception ex)
            {
                RecordingStatus = $"録音停止に失敗しました: {ex.Message}";
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

            recordingWindow = new RecordingWindow(serif)
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
        }

    }
}



