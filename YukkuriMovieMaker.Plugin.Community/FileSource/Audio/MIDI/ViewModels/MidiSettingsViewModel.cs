using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Models;
using YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.Services;

namespace YukkuriMovieMaker.Plugin.Community.FileSource.Audio.MIDI.ViewModels;

internal sealed class MidiSettingsViewModel : Bindable
{
    public MidiPluginSettings Settings => MidiPluginSettings.Default;
    public AudioSettings Audio => Settings.Audio;
    public PerformanceSettings Performance => Settings.Performance;
    public SoundFontSettings SoundFont => Settings.SoundFont;
    public EffectsSettings Effects => Settings.Effects;

    public ObservableCollection<string> InstalledSoundFonts { get; } = [];
    public IReadOnlyList<RenderingMode> AvailableRenderingModes { get; } = Enum.GetValues<RenderingMode>();

    public ActionCommand AddLayerCommand { get; }
    public ActionCommand RemoveLayerCommand { get; }
    public ActionCommand RefreshSoundFontsCommand { get; }

    private SoundFontEntry? selectedLayer;
    public SoundFontEntry? SelectedLayer
    {
        get => selectedLayer;
        set
        {
            if (!Set(ref selectedLayer, value))
                return;
            RemoveLayerCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSoundFonts => InstalledSoundFonts.Count > 0;
    public bool HasNoSoundFonts => !HasSoundFonts;

    public MidiSettingsViewModel()
    {
        AddLayerCommand = new ActionCommand(_ => true, _ => SoundFont.Layers.Add(new SoundFontEntry()));
        RemoveLayerCommand = new ActionCommand(_ => SelectedLayer is not null, _ =>
        {
            if (SelectedLayer is not null)
                SoundFont.Layers.Remove(SelectedLayer);
        });
        RefreshSoundFontsCommand = new ActionCommand(_ => true, _ => RefreshList());
        RefreshList();
    }

    private void RefreshList()
    {
        InstalledSoundFonts.Clear();
        foreach (var f in SoundFontResolverService.GetInstalledFiles())
            InstalledSoundFonts.Add(Path.GetFileName(f));

        var stale = SoundFont.Layers
            .Where(l => !string.IsNullOrEmpty(l.FileName) && !InstalledSoundFonts.Contains(l.FileName, StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var layer in stale)
            SoundFont.Layers.Remove(layer);

        OnPropertyChanged(nameof(HasSoundFonts));
        OnPropertyChanged(nameof(HasNoSoundFonts));
        RemoveLayerCommand.RaiseCanExecuteChanged();
    }
}
