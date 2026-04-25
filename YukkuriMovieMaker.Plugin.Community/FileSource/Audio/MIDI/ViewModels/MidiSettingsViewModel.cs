using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.ViewModels;

internal sealed class MidiSettingsViewModel : INotifyPropertyChanged
{
    private readonly SoundFontDownloadService _downloadService = new();

    public MidiPluginSettings Settings => MidiPluginSettings.Default;
    public AudioSettings Audio => Settings.Audio;
    public PerformanceSettings Performance => Settings.Performance;
    public SoundFontSettings SoundFont => Settings.SoundFont;
    public EffectsSettings Effects => Settings.Effects;

    public ObservableCollection<string> InstalledSoundFonts { get; } = [];
    public IReadOnlyList<RenderingMode> AvailableRenderingModes { get; } = Enum.GetValues<RenderingMode>();

    public ICommand DownloadSoundFontCommand { get; }
    public ICommand AddLayerCommand { get; }
    public ICommand RemoveLayerCommand { get; }
    public ICommand OpenSoundFontFolderCommand { get; }
    public ICommand RefreshSoundFontsCommand { get; }

    private SoundFontEntry? _selectedLayer;
    public SoundFontEntry? SelectedLayer
    {
        get => _selectedLayer;
        set => SetField(ref _selectedLayer, value);
    }

    private bool _isDownloading;
    public bool IsDownloading { get => _isDownloading; private set => SetField(ref _isDownloading, value); }

    private double _downloadProgress;
    public double DownloadProgress { get => _downloadProgress; private set => SetField(ref _downloadProgress, value); }

    private string _downloadStatus = string.Empty;
    public string DownloadStatus { get => _downloadStatus; private set => SetField(ref _downloadStatus, value); }

    public bool HasSoundFonts => InstalledSoundFonts.Count > 0;
    public bool HasNoSoundFonts => !HasSoundFonts;

    public MidiSettingsViewModel()
    {
        DownloadSoundFontCommand = new RelayCommand(async () => await DownloadAsync(), () => !IsDownloading && HasNoSoundFonts);
        AddLayerCommand = new RelayCommand(() => SoundFont.Layers.Add(new SoundFontEntry()));
        RemoveLayerCommand = new RelayCommand(() =>
        {
            if (SelectedLayer is not null)
                SoundFont.Layers.Remove(SelectedLayer);
        }, () => SelectedLayer is not null);
        OpenSoundFontFolderCommand = new RelayCommand(() =>
        {
            SoundFontDownloadService.EnsureDirectory();
            Process.Start("explorer.exe", SoundFontDownloadService.SoundFontDirectory);
        });
        RefreshSoundFontsCommand = new RelayCommand(RefreshList);
        RefreshList();
    }

    private async Task DownloadAsync()
    {
        IsDownloading = true;
        DownloadStatus = Localization.Texts.Downloading;
        DownloadProgress = 0;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var progress = new YukkuriMovieMaker.Commons.ProgressMessage();
            progress.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(progress.Rate))
                    DownloadProgress = progress.Rate >= 0 ? progress.Rate * 100 : 0;
                else if (e.PropertyName == nameof(progress.Message))
                    DownloadStatus = progress.Message;
            };
            await _downloadService.DownloadAsync("GeneralUser-GS.sf2.zip", progress);
            DownloadStatus = Localization.Texts.DownloadComplete;
            RefreshList();
        }
        catch
        {
            DownloadStatus = Localization.Texts.DownloadFailed;
        }
        finally
        {
            IsDownloading = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void RefreshList()
    {
        InstalledSoundFonts.Clear();
        foreach (var f in SoundFontDownloadService.GetInstalledSoundFonts())
            InstalledSoundFonts.Add(Path.GetFileName(f));

        var stale = SoundFont.Layers
            .Where(l => !string.IsNullOrEmpty(l.FileName) && !InstalledSoundFonts.Contains(l.FileName, StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var layer in stale)
            SoundFont.Layers.Remove(layer);

        OnPropertyChanged(nameof(HasSoundFonts));
        OnPropertyChanged(nameof(HasNoSoundFonts));
        CommandManager.InvalidateRequerySuggested();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

file sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _) => execute();
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
