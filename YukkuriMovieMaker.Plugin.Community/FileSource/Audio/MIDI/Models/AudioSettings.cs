using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

public class AudioSettings : INotifyPropertyChanged
{
    private int _sampleRate = 44100;
    public int SampleRate { get => _sampleRate; set => SetField(ref _sampleRate, value); }

    private float _masterVolume = 1.0f;
    public float MasterVolume { get => _masterVolume; set => SetField(ref _masterVolume, value); }

    public void CopyFrom(AudioSettings source)
    {
        SampleRate = source.SampleRate;
        MasterVolume = source.MasterVolume;
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
