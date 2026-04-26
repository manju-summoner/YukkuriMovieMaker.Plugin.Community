using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Views;


namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI;

public class MidiPluginSettings : SettingsBase<MidiPluginSettings>
{
    public override string Name => Localization.Texts.SettingsName;
    public override SettingsCategory Category => SettingsCategory.AudioFileSource;
    public override bool HasSettingView => true;
    public override object? SettingView => new MidiSettingsView();

    public AudioSettings Audio { get => audio; set => Set(ref audio, value); }
    AudioSettings audio = new();

    public PerformanceSettings Performance { get => performance; set => Set(ref performance, value); }
    PerformanceSettings performance = new();

    public SoundFontSettings SoundFont { get => soundFont; set => Set(ref soundFont, value); }
    SoundFontSettings soundFont = new();

    public EffectsSettings Effects { get => effects; set => Set(ref effects, value); }
    EffectsSettings effects = new();

    private ObservableCollection<SoundFontEntry>? _trackedLayers;

    public override void Initialize()
    {
        AttachNestedHandlers();
    }

    private void AttachNestedHandlers()
    {
        audio.PropertyChanged += OnSettingChanged;
        performance.PropertyChanged += OnSettingChanged;
        effects.PropertyChanged += OnSettingChanged;
        soundFont.PropertyChanged += OnSoundFontChanged;
        AttachLayersHandlers(soundFont.Layers);
    }

    private void AttachLayersHandlers(ObservableCollection<SoundFontEntry> layers)
    {
        if (_trackedLayers is not null)
        {
            _trackedLayers.CollectionChanged -= OnLayersCollectionChanged;
            foreach (var entry in _trackedLayers)
                entry.PropertyChanged -= OnLayerEntryChanged;
        }

        _trackedLayers = layers;
        layers.CollectionChanged += OnLayersCollectionChanged;
        foreach (var entry in layers)
            entry.PropertyChanged += OnLayerEntryChanged;
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
    }

    private void OnSoundFontChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
        if (e.PropertyName == nameof(SoundFontSettings.Layers))
            AttachLayersHandlers(soundFont.Layers);
        MidiAudioSource.ClearCache();
    }

    private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SoundFontEntry entry in e.OldItems)
                entry.PropertyChanged -= OnLayerEntryChanged;

        if (e.NewItems is not null)
            foreach (SoundFontEntry entry in e.NewItems)
                entry.PropertyChanged += OnLayerEntryChanged;

        Save();
        MidiAudioSource.ClearCache();
    }

    private void OnLayerEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
        if (e.PropertyName != nameof(SoundFontEntry.Volume))
            MidiAudioSource.ClearCache();
    }

    public string GetConfigurationHash()
    {
        var parts = string.Concat(
            audio.SampleRate, audio.MasterVolume,
            performance.RenderingMode, performance.MaxPolyphony, performance.EnableGpuAcceleration,
            soundFont.EnableSoundFont, soundFont.FallbackToSynthesis,
            string.Join(",", soundFont.Layers.Select(l => $"{l.FileName}:{l.Volume}:{l.IsEnabled}")),
            effects.EnableEffects, effects.EnableReverb, effects.ReverbDecay,
            effects.EnableCompression, effects.CompressionThreshold, effects.CompressionRatio,
            effects.EnableLimiter, effects.LimiterThreshold);

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(parts));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
