using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

public sealed class GrdIndexSelectorViewModel : INotifyPropertyChanged
{
    private bool _suppressSync;
    private GrdManifest _manifest = GrdManifest.Empty;

    public ObservableCollection<GrdGradientEntry> Entries { get; } = [];

    public bool IsVisible => _manifest.IsMultiple;

    public GrdGradientEntry? SelectedEntry
    {
        get
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Index == GradientIndex)
                    return Entries[i];
            }
            return null;
        }
        set
        {
            if (value is null) return;
            if (GradientIndex == value.Index) return;
            GradientIndex = value.Index;
            OnPropertyChanged();
            if (!_suppressSync)
                OnPropertyChanged(nameof(GradientIndex));
        }
    }

    public int GradientIndex
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SyncSelection();
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => field;
        set
        {
            if (string.Equals(field, value, StringComparison.OrdinalIgnoreCase)) return;
            field = value;
            RefreshManifest();
        }
    } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RefreshManifest()
    {
        _manifest = GradientTextureFactory.ReadManifest(FilePath);

        Entries.Clear();
        for (var i = 0; i < _manifest.Gradients.Length; i++)
            Entries.Add(_manifest.Gradients[i]);

        _suppressSync = true;
        try
        {
            var clamped = _manifest.Count > 0
                ? Math.Clamp(GradientIndex, 0, _manifest.Count - 1)
                : 0;
            GradientIndex = clamped;
            SyncSelection();
        }
        finally
        {
            _suppressSync = false;
        }

        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(GradientIndex));
    }

    private void SyncSelection() => OnPropertyChanged(nameof(SelectedEntry));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(name!));
}
