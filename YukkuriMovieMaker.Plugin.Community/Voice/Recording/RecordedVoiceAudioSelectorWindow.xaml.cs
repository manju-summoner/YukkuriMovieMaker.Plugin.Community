using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using NAudio.Wave;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Models;
using YukkuriMovieMaker.Plugin.Community.Tool.Recording.Services;
using YukkuriMovieMaker.Settings;
using ToolTexts = YukkuriMovieMaker.Plugin.Community.Tool.Recording.Texts;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public partial class RecordedVoiceAudioSelectorWindow : Window, INotifyPropertyChanged
    {
        private WaveOutEvent? player;
        private AudioFileReader? reader;

        private readonly RecordPathService recordPathService;
        private readonly RecordingService recordingService;

        public ObservableCollection<RecordedAudioListItem> Items { get; } = [];
        public ObservableCollection<RecordingDeviceInfo> AvailableDevices { get; } = [];

        private RecordedAudioListItem? selectedItem;
        public RecordedAudioListItem? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (ReferenceEquals(selectedItem, value))
                    return;
                selectedItem = value;
                Raise(nameof(SelectedItem));
                UpdatePreviewForSelectionChange();
            }
        }

        public string SelectedPath { get; private set; } = string.Empty;

        private string recordsDirectory = string.Empty;
        public string RecordsDirectory => recordsDirectory;

        public string OutputDirectory
        {
            get => recordsDirectory;
            set
            {
                if (IsRecording)
                    return;

                var normalized = string.IsNullOrWhiteSpace(value)
                    ? RecordingSettings.GetDefaultOutputDirectory()
                    : value.Trim();

                if (string.Equals(recordsDirectory, normalized, StringComparison.Ordinal))
                    return;

                recordsDirectory = normalized;
                Raise(nameof(OutputDirectory));
                Raise(nameof(RecordsDirectory));
                LoadItems(recordsDirectory, SelectedPath);
            }
        }

        private string? selectedDeviceId;
        public string? SelectedDeviceId
        {
            get => selectedDeviceId;
            set
            {
                if (string.Equals(selectedDeviceId, value, StringComparison.Ordinal))
                    return;
                selectedDeviceId = value;
                RecordingSettings.Default.SelectedRecordingDeviceId = value ?? RecordingService.DefaultRecordingDeviceId;
                Raise(nameof(SelectedDeviceId));
            }
        }

        private double currentVolume;
        public double CurrentVolume
        {
            get => currentVolume;
            set
            {
                if (currentVolume == value)
                    return;
                currentVolume = value;
                Raise(nameof(CurrentVolume));
            }
        }

        public bool IsPlaying => player is not null;
        public bool IsRecording => recordingService.IsRecording;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public RecordedVoiceAudioSelectorWindow(string? recordsDirectory, string? currentPath)
        {
            InitializeComponent();
            this.recordsDirectory = string.IsNullOrWhiteSpace(recordsDirectory)
                ? RecordingSettings.Default.OutputDirectory
                : recordsDirectory;

            recordPathService = new RecordPathService(() => this.recordsDirectory);
            recordingService = new RecordingService(recordPathService);
            recordingService.DataAvailable += OnRecordingDataAvailable;
            recordingService.RecordingStateChanged += OnRecordingStateChanged;

            selectedDeviceId = string.IsNullOrWhiteSpace(RecordingSettings.Default.SelectedRecordingDeviceId)
                ? RecordingService.DefaultRecordingDeviceId
                : RecordingSettings.Default.SelectedRecordingDeviceId;

            RefreshMicrophones();
            LoadItems(this.recordsDirectory, currentPath);
            DataContext = this;
        }

        private void RefreshMicrophones()
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
                        ? ToolTexts.DefaultRecordingDevice
                        : string.Format(ToolTexts.DefaultRecordingDeviceWithName, defaultDeviceName),
                    ResolvedDeviceName = defaultDeviceName,
                });
                foreach (var device in recordingService.GetAvailableDevices())
                    AvailableDevices.Add(device);

                if (AvailableDevices.All(x => !string.Equals(x.Id, SelectedDeviceId, StringComparison.Ordinal)))
                    SelectedDeviceId = RecordingService.DefaultRecordingDeviceId;
            }
            catch
            {
                AvailableDevices.Clear();
            }
        }

        private void OnRefreshDevicesClicked(object sender, RoutedEventArgs e) => RefreshMicrophones();

        private void OnBrowseOutputClicked(object sender, RoutedEventArgs e)
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = ToolTexts.SelectOutputDirectory,
                UseDescriptionForTitle = true,
                SelectedPath = OutputDirectory,
            };
            if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                OutputDirectory = dialog.SelectedPath;
        }

        private void OnResetOutputClicked(object sender, RoutedEventArgs e)
        {
            OutputDirectory = RecordingSettings.GetDefaultOutputDirectory();
        }

        private void OnRecordingDataAvailable(object? sender, RecordingDataEventArgs e)
        {
            Dispatcher.BeginInvoke(() => CurrentVolume = e.Volume);
        }

        private void OnRecordingStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Raise(nameof(IsRecording));
                if (!IsRecording)
                    CurrentVolume = 0;
            });
        }

        private void LoadItems(string? recordsDirectory, string? currentPath)
        {
            Items.Clear();
            if (string.IsNullOrWhiteSpace(recordsDirectory) || !Directory.Exists(recordsDirectory))
                return;

            var items = Directory.EnumerateFiles(recordsDirectory, "*.wav", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .Select(x => RecordedAudioListItem.Create(x.FullName, x.Name, x.CreationTime))
                .ToList();

            foreach (var item in items)
                Items.Add(item);

            SelectedItem = Items.FirstOrDefault(x => string.Equals(x.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                ?? Items.FirstOrDefault();
        }

        private void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is null)
                return;

            SelectedPath = SelectedItem.Path;
            DialogResult = true;
        }

        private void OnUnselectClicked(object sender, RoutedEventArgs e)
        {
            SelectedPath = RecordedVoiceParameter.ExplicitUnselectedToken;
            DialogResult = true;
        }

        private void OnPreviewClicked(object sender, RoutedEventArgs e)
        {
            if (IsPlaying)
                StopPlayback();
            else
                PlaySelected();
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is null)
                return;

            var target = SelectedItem;
            var result = MessageBox.Show(
                this,
                string.Format(ToolTexts.ConfirmDeleteRecord, target.FileName),
                Texts.RecordedAudioSelectorTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            StopPlayback();

            try
            {
                if (File.Exists(target.Path))
                    File.Delete(target.Path);
                LoadItems(OutputDirectory, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(ToolTexts.RecordDeleteFailed, ex.Message), Texts.RecordedAudioSelectorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void OnRecordClicked(object sender, RoutedEventArgs e)
        {
            if (IsRecording)
            {
                try
                {
                    var info = await recordingService.StopRecordingAsync();
                    if (info is not null && info.DataLength > 0)
                        LoadItems(OutputDirectory, info.FilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, string.Format(Texts.RecordingFailed, ex.Message), Texts.RecordedAudioSelectorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            StopPlayback();
            try
            {
                recordingService.StartRecording(SelectedDeviceId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Texts.RecordingFailed, ex.Message), Texts.RecordedAudioSelectorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnListViewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedItem is null)
                return;

            SelectedPath = SelectedItem.Path;
            DialogResult = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPlayback();
            recordingService.DataAvailable -= OnRecordingDataAvailable;
            recordingService.RecordingStateChanged -= OnRecordingStateChanged;
            try
            {
                if (recordingService.IsRecording)
                    recordingService.StopRecording();
            }
            catch { /* shutting down */ }
            recordingService.Dispose();
            base.OnClosed(e);
        }

        private void StopPlayback()
        {
            var currentPlayer = player;
            var currentReader = reader;
            player = null;
            reader = null;

            if (currentPlayer is not null)
            {
                currentPlayer.PlaybackStopped -= OnPlaybackStopped;
                currentPlayer.Stop();
                currentPlayer.Dispose();
            }

            currentReader?.Dispose();
            Raise(nameof(IsPlaying));
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (player is not null)
            {
                player.PlaybackStopped -= OnPlaybackStopped;
                player.Dispose();
                player = null;
            }

            reader?.Dispose();
            reader = null;
            Raise(nameof(IsPlaying));
        }

        private void UpdatePreviewForSelectionChange()
        {
            if (player is null)
                return;

            if (SelectedItem is null || !File.Exists(SelectedItem.Path))
            {
                StopPlayback();
                return;
            }

            PlaySelected();
        }

        private void PlaySelected()
        {
            if (SelectedItem is null || !File.Exists(SelectedItem.Path))
                return;

            StopPlayback();

            try
            {
                reader = new AudioFileReader(SelectedItem.Path);
                player = new WaveOutEvent();
                player.Volume = GetYmmVolume();
                player.PlaybackStopped += OnPlaybackStopped;
                player.Init(reader);
                player.Play();
                Raise(nameof(IsPlaying));
            }
            catch
            {
                // Keep the selector usable even if the file is temporarily locked or invalid.
                StopPlayback();
            }
        }

        static float GetYmmVolume()
        {
            var settings = YMMSettings.Default;
            if (settings.IsMuted)
                return 0f;
            return (float)Math.Clamp(settings.Volume / 100.0, 0.0, 1.0);
        }
    }

    public sealed class RecordedAudioListItem
    {
        public string Path { get; }
        public string FileName { get; }
        public DateTime CreatedAt { get; }
        public TimeSpan? Duration { get; }
        public string CreatedAtText => CreatedAt.ToString("yyyy/MM/dd HH:mm:ss");
        public string DurationText => Duration?.ToString(@"mm\:ss") ?? "--:--";

        private RecordedAudioListItem(string path, string fileName, DateTime createdAt, TimeSpan? duration)
        {
            Path = path;
            FileName = fileName;
            CreatedAt = createdAt;
            Duration = duration;
        }

        public static RecordedAudioListItem Create(string path, string fileName, DateTime createdAt)
        {
            TimeSpan? duration = null;
            try
            {
                using var reader = new AudioFileReader(path);
                duration = reader.TotalTime;
            }
            catch
            {
                // Duration may be unavailable for broken/locked files; fallback to "--:--".
            }

            return new RecordedAudioListItem(path, fileName, createdAt, duration);
        }
    }
}
