using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public class ToolViewModel : INotifyPropertyChanged, ITimelineToolViewModel
    {
        private readonly RecordingService recordingService;
        private readonly TimelineInsertService timelineInsertService;
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
                selectedDevice = value;
                OnPropertyChanged(nameof(SelectedDevice));
                RaiseCommandStates();
            }
        }

        private string recordingStatus = "待機中";
        public string RecordingStatus
        {
            get => recordingStatus;
            set
            {
                recordingStatus = value;
                OnPropertyChanged(nameof(RecordingStatus));
            }
        }

        private double currentVolume;
        public double CurrentVolume
        {
            get => currentVolume;
            set
            {
                currentVolume = value;
                OnPropertyChanged(nameof(CurrentVolume));
            }
        }

        private string recordsDirectory = string.Empty;
        public string RecordsDirectory
        {
            get => recordsDirectory;
            set
            {
                recordsDirectory = value;
                OnPropertyChanged(nameof(RecordsDirectory));
            }
        }

        public bool IsRecording => recordingService.IsRecording;

        public static Timeline? TimelineInstance { get; private set; }
        public Timeline Timeline { get; set; } = null!;

        public ToolViewModel()
        {
            var recordPathService = new RecordPathService(RecordingSettings.Default.OutputDirectory);
            recordingService = new RecordingService(recordPathService);
            timelineInsertService = new TimelineInsertService();

            recordingService.DataAvailable += OnRecordingDataAvailable;
            recordingService.RecordingStateChanged += OnRecordingStateChanged;

            StartRecordingCommand = new ActionCommand(_ => CanStartRecording(), _ => StartRecording());
            StopRecordingCommand = new ActionCommand(_ => CanStopRecording(), _ => StopRecording());
            RefreshDevicesCommand = new ActionCommand(_ => true, _ => RefreshMicrophones());
            OpenRecordingWindowCommand = new ActionCommand(_ => CanOpenRecordingWindow(), _ => OpenRecordingWindow());

            RecordsDirectory = recordPathService.GetRecordsDirectory();
            RefreshMicrophones();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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
            if (SelectedDevice is null)
            {
                RecordingStatus = "録音デバイスを再選択してください。";
                return;
            }

            try
            {
                recordingService.StartRecording(SelectedDevice);
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
                var recordedFile = await recordingService.StopRecordingAsync();

                CurrentVolume = 0;
                RaiseCommandStates();

                if (recordedFile is null)
                {
                    RecordingStatus = "録音データがありません。再録音してください。";
                    return;
                }

                if (recordedFile.DataLength <= 0)
                {
                    RecordingStatus = $"録音データ長が 0 のため追加しませんでした: {recordedFile.FilePath}";
                    return;
                }

                RecordingStatus = $"録音停止。タイムラインへ追加中... {recordedFile.FilePath}";

                await timelineInsertService.InsertAsync(recordedFile);
                RecordingStatus = $"録音停止。タイムラインへ追加しました: {recordedFile.FilePath}";
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

            var selectionService = new TimelineSelectionService();
            var serif = selectionService.TryGetSelectedSerif();

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
            CurrentVolume = e.Volume;
        }

        private void OnRecordingStateChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsRecording));
            RaiseCommandStates();
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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}



