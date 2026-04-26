using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

public class SoundFontEntry : INotifyPropertyChanged, ICloneable
{
    private string _fileName = string.Empty;
    public string FileName { get => _fileName; set => SetField(ref _fileName, value); }

    private float _volume = 1.0f;
    public float Volume { get => _volume; set => SetField(ref _volume, value); }

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set => SetField(ref _isEnabled, value); }

    public object Clone() => new SoundFontEntry
    {
        FileName = FileName,
        Volume = Volume,
        IsEnabled = IsEnabled,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
