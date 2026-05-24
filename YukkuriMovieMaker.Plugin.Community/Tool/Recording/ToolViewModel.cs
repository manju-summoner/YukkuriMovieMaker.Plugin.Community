using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.ViewModels;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Recording
{
    public class ToolViewModel : Bindable, IDisposable
    {
        public bool CanSuspend => !IsRecording && !audioPlaybackService.IsPlaying;

        readonly RecordingService recordingService;
        readonly ToolRecordingStartWorkflowService recordingStartWorkflowService;
        readonly ToolRecordingStopWorkflowService recordingStopWorkflowService;
        readonly AudioPlaybackService audioPlaybackService;
        readonly RecordPathService recordPathService;
        bool disposed;
        string? latestRecordedFilePath;

        public ObservableCollection<RecordingDeviceInfo> AvailableDevices { get; } = [];
        public ObservableCollection<RecordedFileListItem> Records { get; } = [];
        public MessageBoxViewModel MessageBoxViewModel { get; } = new();

        RenameRecordViewModel? renameRecord;
        public RenameRecordViewModel? RenameRecord
        {
            get => renameRecord;
            set => Set(ref renameRecord, value);
        }

        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand BrowseOutputDirectoryCommand { get; }
        public ICommand ResetOutputDirectoryCommand { get; }
        public ICommand RefreshRecordsCommand { get; }
        public ICommand PlaySelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand RenameSelectedCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand CopyPathCommand { get; }

        string? selectedDeviceId;
        public string? SelectedDeviceId
        {
            get => selectedDeviceId;
            set
            {
                if (Set(ref selectedDeviceId, value))
                {
                    RecordingSettings.Default.SelectedRecordingDeviceId = value ?? RecordingService.DefaultRecordingDeviceId;
                    RaiseCommandStates();
                }
            }
        }

        RecordedFileListItem? selectedRecord;
        public RecordedFileListItem? SelectedRecord
        {
            get => selectedRecord;
            set
            {
                if (Set(ref selectedRecord, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        string recordingStatus = Texts.Idle;
        public string RecordingStatus
        {
            get => recordingStatus;
            set => Set(ref recordingStatus, value);
        }

        double currentVolume;
        public double CurrentVolume
        {
            get => currentVolume;
            set => Set(ref currentVolume, value);
        }

        string recordsDirectory = string.Empty;
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
                RefreshRecords();
            }
        }

        public bool IsRecording => recordingService.IsRecording;
        public bool IsPlaying => audioPlaybackService.IsPlaying;

        public ToolViewModel()
        {
            recordPathService = new RecordPathService();
            recordingService = new RecordingService(recordPathService);
            recordingStartWorkflowService = new ToolRecordingStartWorkflowService(recordingService);
            recordingStopWorkflowService = new ToolRecordingStopWorkflowService(recordingService);
            audioPlaybackService = new AudioPlaybackService();

            recordingService.DataAvailable += OnRecordingDataAvailable;
            recordingService.RecordingStateChanged += OnRecordingStateChanged;
            audioPlaybackService.PlaybackStopped += OnPlaybackStopped;

            StartRecordingCommand = new ActionCommand(_ => CanStartRecording(), _ => StartRecording());
            StopRecordingCommand = new ActionCommand(_ => IsRecording, _ => StopRecording());
            RefreshDevicesCommand = new ActionCommand(_ => true, _ => RefreshMicrophones());
            BrowseOutputDirectoryCommand = new ActionCommand(_ => !IsRecording, _ => BrowseOutputDirectory());
            ResetOutputDirectoryCommand = new ActionCommand(_ => CanResetOutputDirectory(), _ => ResetOutputDirectory());
            RefreshRecordsCommand = new ActionCommand(_ => true, _ => RefreshRecords());
            PlaySelectedCommand = new ActionCommand(_ => CanPlaySelected(), _ => PlaySelected());
            DeleteSelectedCommand = new ActionCommand(_ => CanDeleteSelected(), _ => DeleteSelected());
            RenameSelectedCommand = new ActionCommand(_ => CanRenameSelected(), _ => RenameSelected());
            OpenFolderCommand = new ActionCommand(_ => Directory.Exists(OutputDirectory), _ => OpenFolder());
            CopyPathCommand = new ActionCommand(_ => SelectedRecord is not null, _ => CopyPath());

            InitializeRecordsDirectory();
            SelectedDeviceId = string.IsNullOrWhiteSpace(RecordingSettings.Default.SelectedRecordingDeviceId)
                ? RecordingService.DefaultRecordingDeviceId
                : RecordingSettings.Default.SelectedRecordingDeviceId;
            RefreshMicrophones();
            RefreshRecords();
        }

        void RefreshMicrophones()
        {
            try
            {
                AvailableDevices.Clear();

                var defaultDeviceName = recordingService.GetDefaultRecordingDeviceFriendlyName();
                AvailableDevices.Add(new RecordingDeviceInfo
                {
                    Id = RecordingService.DefaultRecordingDeviceId,
                    IsDefault = true,
                    FriendlyName = string.IsNullOrWhiteSpace(defaultDeviceName)
                        ? Texts.DefaultRecordingDevice
                        : string.Format(Texts.DefaultRecordingDeviceWithName, defaultDeviceName),
                    ResolvedDeviceName = defaultDeviceName,
                });

                foreach (var device in recordingService.GetAvailableDevices())
                    AvailableDevices.Add(device);

                if (AvailableDevices.Count > 1 || !string.IsNullOrWhiteSpace(defaultDeviceName))
                {
                    if (string.IsNullOrWhiteSpace(SelectedDeviceId))
                    {
                        SelectedDeviceId = AvailableDevices[0].Id;
                    }
                    else if (AvailableDevices.All(x => !string.Equals(x.Id, SelectedDeviceId, StringComparison.Ordinal)))
                    {
                        if (!string.Equals(SelectedDeviceId, RecordingService.DefaultRecordingDeviceId, StringComparison.Ordinal))
                            RecordingStatus = Texts.SavedRecordingDeviceNotFoundFallback;

                        SelectedDeviceId = RecordingService.DefaultRecordingDeviceId;
                    }
                }
                else
                {
                    SelectedDeviceId = null;
                    RecordingStatus = Texts.NoRecordingDeviceFoundDetailed;
                }
            }
            catch (Exception ex)
            {
                AvailableDevices.Clear();
                SelectedDeviceId = null;
                RecordingStatus = string.Format(Texts.RecordingStartFailed, ex.Message);
            }

            RaiseCommandStates();
        }

        void RefreshRecords()
        {
            try
            {
                var directory = recordPathService.GetRecordsDirectory();
                RecordsDirectory = directory;

                var items = Directory.EnumerateFiles(directory, "*.wav", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(x => x.LastWriteTimeUtc)
                    .Select(x => new RecordedFileListItem(x.FullName, x.Name, x.LastWriteTime, x.Length))
                    .ToList();

                Records.Clear();
                foreach (var item in items)
                    Records.Add(item);

                SelectedRecord = Records.FirstOrDefault(x => string.Equals(x.Path, latestRecordedFilePath, StringComparison.OrdinalIgnoreCase))
                    ?? Records.FirstOrDefault();

                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordListRefreshFailed, ex.Message);
            }
        }

        void StartRecording()
        {
            try
            {
                if (!EnsureOutputDirectory())
                    return;

                if (audioPlaybackService.IsPlaying)
                    audioPlaybackService.Stop();

                var startResult = recordingStartWorkflowService.Execute(SelectedDeviceId);
                if (!startResult.IsSuccess)
                {
                    RecordingStatus = startResult.ErrorMessage ?? Texts.ReselectRecordingDevice;
                    return;
                }

                latestRecordedFilePath = null;
                var recordingStatusMessage = string.Format(Texts.RecordingNowWithPathAndDevice, RecordsDirectory, startResult.Selection.FriendlyName);
                if (startResult.Selection.FellBackToDefault)
                {
                    var fallbackMessage = string.Format(Texts.SavedRecordingDeviceNotFoundFallbackWithName, startResult.Selection.FriendlyName);
                    recordingStatusMessage = $"{fallbackMessage}{Environment.NewLine}{recordingStatusMessage}";
                }

                RecordingStatus = recordingStatusMessage;
                RaiseCommandStates();
            }
            catch (InvalidOperationException ex)
            {
                RecordingStatus = ex.Message;
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordingStartFailed, ex.Message);
                RaiseCommandStates();
            }
        }

        async void StopRecording()
        {
            try
            {
                var stopResult = await recordingStopWorkflowService.ExecuteAsync();

                CurrentVolume = 0;

                if (!stopResult.HasData)
                {
                    RecordingStatus = Texts.NoRecordingDataRetry;
                    RaiseCommandStates();
                    return;
                }

                if (stopResult.HasZeroLengthData)
                {
                    RecordingStatus = string.Format(Texts.RecordingDataLengthZero, stopResult.FilePath ?? string.Empty);
                    RaiseCommandStates();
                    return;
                }

                latestRecordedFilePath = stopResult.FilePath;
                RecordingStatus = string.Format(Texts.RecordingStoppedAndSaved, stopResult.FilePath ?? string.Empty);
                RefreshRecords();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordingStopFailed, ex.Message);
                RaiseCommandStates();
            }
        }

        bool EnsureOutputDirectory()
        {
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                return true;
            }
            catch (Exception)
            {
                var fallback = RecordingSettings.GetDefaultOutputDirectory();
                if (string.Equals(OutputDirectory, fallback, StringComparison.OrdinalIgnoreCase))
                {
                    RecordingStatus = Texts.OutputFolderUnavailable;
                    return false;
                }

                try
                {
                    Directory.CreateDirectory(fallback);
                    OutputDirectory = fallback;
                    MessageBoxViewModel.Show(
                        string.Format(Texts.OutputDirectoryFallback, fallback),
                        Texts.ToolName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning,
                        MessageBoxResult.OK);
                    return true;
                }
                catch (Exception)
                {
                    RecordingStatus = Texts.OutputFolderUnavailable;
                    return false;
                }
            }
        }

        void InitializeRecordsDirectory()
        {
            try
            {
                RecordsDirectory = recordPathService.GetRecordsDirectory();
            }
            catch (Exception)
            {
                var fallback = RecordingSettings.GetDefaultOutputDirectory();
                try
                {
                    Directory.CreateDirectory(fallback);
                    OutputDirectory = fallback;
                    RecordingStatus = string.Format(Texts.OutputDirectoryFallback, fallback);
                }
                catch (Exception)
                {
                    RecordsDirectory = fallback;
                    RecordingStatus = Texts.OutputFolderUnavailable;
                }
            }
        }

        bool CanStartRecording() => !IsRecording && !string.IsNullOrWhiteSpace(SelectedDeviceId);
        bool CanResetOutputDirectory() => !IsRecording && !string.Equals(
            OutputDirectory,
            RecordingSettings.GetDefaultOutputDirectory(),
            StringComparison.OrdinalIgnoreCase);
        bool CanPlaySelected() => !IsRecording
            && (audioPlaybackService.IsPlaying
                || (SelectedRecord is not null && File.Exists(SelectedRecord.Path)));
        bool CanDeleteSelected() => !IsRecording && !audioPlaybackService.IsPlaying && SelectedRecord is not null;
        bool CanRenameSelected() => !IsRecording && SelectedRecord is not null;

        void PlaySelected()
        {
            if (audioPlaybackService.IsPlaying)
            {
                audioPlaybackService.Stop();
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(CanSuspend));
                RaiseCommandStates();
                return;
            }

            if (SelectedRecord is null)
                return;
            PlayFile(SelectedRecord.Path);
        }

        void PlayFile(string path)
        {
            try
            {
                audioPlaybackService.Play(path);
                RecordingStatus = Texts.Playing;
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(CanSuspend));
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.PlaybackFailed, ex.Message);
                audioPlaybackService.Stop();
                OnPropertyChanged(nameof(IsPlaying));
            }
            finally
            {
                RaiseCommandStates();
            }
        }

        void DeleteSelected()
        {
            if (SelectedRecord is null)
                return;

            var target = SelectedRecord;
            var confirm = MessageBoxViewModel.Show(
                string.Format(Texts.ConfirmDeleteRecord, target.FileName),
                Texts.ToolName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                if (File.Exists(target.Path))
                    File.Delete(target.Path);

                RecordingStatus = string.Format(Texts.RecordDeleted, target.FileName);
                if (string.Equals(latestRecordedFilePath, target.Path, StringComparison.OrdinalIgnoreCase))
                    latestRecordedFilePath = null;
                RefreshRecords();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordDeleteFailed, ex.Message);
            }
        }

        void RenameSelected()
        {
            if (SelectedRecord is null)
                return;

            // DialogBehavior は IsModal=True のとき Content の setter で同期的に ShowDialog を呼ぶ。
            var dialogVm = new RenameRecordViewModel(SelectedRecord.FileNameWithoutExtension);
            RenameRecord = dialogVm;
            RenameRecord = null;

            if (dialogVm.Confirmed)
                Rename(dialogVm.NewName);
        }

        void Rename(string newName)
        {
            if (SelectedRecord is null)
                return;

            var safeName = string.Join("_", newName.Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(safeName))
            {
                RecordingStatus = Texts.InvalidFileName;
                return;
            }

            var sourcePath = SelectedRecord.Path;
            var destinationPath = Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty, safeName + ".wav");
            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                File.Move(sourcePath, destinationPath, overwrite: false);
                if (string.Equals(latestRecordedFilePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                    latestRecordedFilePath = destinationPath;

                RecordingStatus = string.Format(Texts.RecordRenamed, Path.GetFileName(destinationPath));
                RefreshRecords();
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.RecordRenameFailed, ex.Message);
            }
        }

        void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = OutputDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.OpenFolderFailed, ex.Message);
            }
        }

        void CopyPath()
        {
            if (SelectedRecord is null)
                return;

            try
            {
                Clipboard.SetText(SelectedRecord.Path);
                RecordingStatus = Texts.RecordPathCopied;
            }
            catch (Exception ex)
            {
                RecordingStatus = string.Format(Texts.CopyPathFailed, ex.Message);
            }
        }

        public string[] GetSelectedRecordPathsForDragDrop()
        {
            if (SelectedRecord is null || !File.Exists(SelectedRecord.Path))
                return [];
            return [SelectedRecord.Path];
        }

        void BrowseOutputDirectory()
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

        void ResetOutputDirectory()
        {
            var confirm = MessageBoxViewModel.Show(
                Texts.ConfirmResetOutputDirectory,
                Texts.ToolName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes)
                return;

            OutputDirectory = RecordingSettings.GetDefaultOutputDirectory();
            RecordingStatus = string.Format(Texts.OutputDirectoryReset, OutputDirectory);
        }

        void OnRecordingDataAvailable(object? sender, Models.RecordingDataEventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                CurrentVolume = e.Volume;
                return;
            }

            _ = dispatcher.BeginInvoke((Action)(() => CurrentVolume = e.Volume));
        }

        void OnRecordingStateChanged(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                if (!disposed)
                {
                    OnPropertyChanged(nameof(IsRecording));
                    OnPropertyChanged(nameof(CanSuspend));
                    RaiseCommandStates();
                }
                return;
            }

            _ = dispatcher.BeginInvoke((Action)(() =>
            {
                if (disposed) return;
                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(CanSuspend));
                RaiseCommandStates();
            }));
        }

        void OnPlaybackStopped(object? sender, NAudio.Wave.StoppedEventArgs e)
        {
            void HandlePlaybackStopped()
            {
                RecordingStatus = e.Exception is null
                    ? Texts.PlaybackCompleted
                    : string.Format(Texts.PlaybackError, e.Exception.Message);
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(CanSuspend));
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

        void RaiseCommandStates()
        {
            (StartRecordingCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (StopRecordingCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (BrowseOutputDirectoryCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (ResetOutputDirectoryCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (RefreshRecordsCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (PlaySelectedCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (DeleteSelectedCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (RenameSelectedCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (OpenFolderCommand as ActionCommand)?.RaiseCanExecuteChanged();
            (CopyPathCommand as ActionCommand)?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            recordingService.DataAvailable -= OnRecordingDataAvailable;
            recordingService.RecordingStateChanged -= OnRecordingStateChanged;
            audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;
            RecordingLifecycleService.TryStopRecording(recordingService);
            recordingService.Dispose();
            audioPlaybackService.Dispose();
        }
    }

    public class RecordedFileListItem
    {
        public RecordedFileListItem(string path, string fileName, DateTime updatedAt, long fileSize)
        {
            Path = path;
            FileName = fileName;
            UpdatedAt = updatedAt;
            FileSize = fileSize;
        }

        public string Path { get; }
        public string FileName { get; }
        public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(FileName);
        public DateTime UpdatedAt { get; }
        public long FileSize { get; }
        public string UpdatedAtText => UpdatedAt.ToString("yyyy/MM/dd HH:mm:ss");
        public string FileSizeText => $"{FileSize / 1024d:F1} KB";
    }
}
