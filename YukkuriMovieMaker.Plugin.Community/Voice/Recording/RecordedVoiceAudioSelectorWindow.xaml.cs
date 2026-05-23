using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using NAudio.Wave;

namespace YukkuriMovieMaker.Plugin.Community.Voice.Recording
{
    public partial class RecordedVoiceAudioSelectorWindow : Window, INotifyPropertyChanged
    {
        private WaveOutEvent? player;
        private AudioFileReader? reader;

        public ObservableCollection<RecordedAudioListItem> Items { get; } = [];
        private RecordedAudioListItem? selectedItem;
        public RecordedAudioListItem? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (ReferenceEquals(selectedItem, value))
                    return;
                selectedItem = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
            }
        }
        public string SelectedPath { get; private set; } = string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;

        public RecordedVoiceAudioSelectorWindow(string? recordsDirectory, string? currentPath)
        {
            InitializeComponent();
            LoadItems(recordsDirectory, currentPath);
            DataContext = this;
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

        private void OnPreviewClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is null || !File.Exists(SelectedItem.Path))
                return;

            StopPlayback();

            try
            {
                reader = new AudioFileReader(SelectedItem.Path);
                player = new WaveOutEvent();
                player.PlaybackStopped += OnPlaybackStopped;
                player.Init(reader);
                player.Play();
            }
            catch
            {
                // Keep the selector usable even if the file is temporarily locked or invalid.
                StopPlayback();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPlayback();
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
