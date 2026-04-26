using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

public class SoundFontSettings : INotifyPropertyChanged
{
    private bool _enableSoundFont = true;
    public bool EnableSoundFont { get => _enableSoundFont; set => SetField(ref _enableSoundFont, value); }

    private bool _fallbackToSynthesis = true;
    public bool FallbackToSynthesis { get => _fallbackToSynthesis; set => SetField(ref _fallbackToSynthesis, value); }

    private ObservableCollection<SoundFontEntry> _layers = [];
    public ObservableCollection<SoundFontEntry> Layers { get => _layers; set => SetField(ref _layers, value); }

    public void CopyFrom(SoundFontSettings source)
    {
        EnableSoundFont = source.EnableSoundFont;
        FallbackToSynthesis = source.FallbackToSynthesis;
        Layers = new ObservableCollection<SoundFontEntry>(source.Layers.Select(l => (SoundFontEntry)l.Clone()));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
