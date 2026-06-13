using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;

public class EffectsSettings : INotifyPropertyChanged
{
    private bool _enableEffects = true;
    public bool EnableEffects { get => _enableEffects; set => SetField(ref _enableEffects, value); }

    private bool _enableReverb = false;
    public bool EnableReverb { get => _enableReverb; set => SetField(ref _enableReverb, value); }

    private float _reverbDecay = 0.3f;
    public float ReverbDecay { get => _reverbDecay; set => SetField(ref _reverbDecay, value); }

    private bool _enableCompression = false;
    public bool EnableCompression { get => _enableCompression; set => SetField(ref _enableCompression, value); }

    private float _compressionThreshold = 0.8f;
    public float CompressionThreshold { get => _compressionThreshold; set => SetField(ref _compressionThreshold, value); }

    private float _compressionRatio = 4.0f;
    public float CompressionRatio { get => _compressionRatio; set => SetField(ref _compressionRatio, value); }

    private bool _enableLimiter = true;
    public bool EnableLimiter { get => _enableLimiter; set => SetField(ref _enableLimiter, value); }

    private float _limiterThreshold = 0.95f;
    public float LimiterThreshold { get => _limiterThreshold; set => SetField(ref _limiterThreshold, value); }

    public void CopyFrom(EffectsSettings source)
    {
        EnableEffects = source.EnableEffects;
        EnableReverb = source.EnableReverb;
        ReverbDecay = source.ReverbDecay;
        EnableCompression = source.EnableCompression;
        CompressionThreshold = source.CompressionThreshold;
        CompressionRatio = source.CompressionRatio;
        EnableLimiter = source.EnableLimiter;
        LimiterThreshold = source.LimiterThreshold;
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
