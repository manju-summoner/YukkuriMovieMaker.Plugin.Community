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

    public override void Initialize()
    {
        AttachNestedHandlers();
    }

    private void AttachNestedHandlers()
    {
        audio.PropertyChanged += OnNestedChanged;
        performance.PropertyChanged += OnNestedChanged;
        soundFont.PropertyChanged += OnNestedChanged;
        effects.PropertyChanged += OnNestedChanged;
    }

    private void OnNestedChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
        MidiAudioSource.ClearCache();
    }

    public string GetConfigurationHash()
    {
        var parts = string.Concat(
            audio.SampleRate, audio.MasterVolume, audio.EnableNormalization, audio.NormalizationLevel,
            performance.RenderingMode, performance.BufferSize, performance.MaxPolyphony, performance.EnableGpuAcceleration,
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
