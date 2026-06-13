using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

public class PerformanceSettings : INotifyPropertyChanged
{
    private RenderingMode _renderingMode = RenderingMode.SoundFont;
    public RenderingMode RenderingMode { get => _renderingMode; set => SetField(ref _renderingMode, value); }

    private int _maxPolyphony = 256;
    public int MaxPolyphony { get => _maxPolyphony; set => SetField(ref _maxPolyphony, value); }

    private bool _enableGpuAcceleration = true;
    public bool EnableGpuAcceleration { get => _enableGpuAcceleration; set => SetField(ref _enableGpuAcceleration, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
