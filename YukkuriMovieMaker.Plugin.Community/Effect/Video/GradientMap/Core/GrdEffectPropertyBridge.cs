using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Views;
using System.ComponentModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Effect;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;

internal sealed class GrdEffectPropertyBridge : IDisposable
{
    private readonly WeakReference<GrdIndexSelector> _selectorRef;
    private readonly List<GradientMapEffect> _effects = [];
    private bool _disposed;

    private GrdEffectPropertyBridge(GrdIndexSelector selector, IReadOnlyList<GradientMapEffect> effects)
    {
        _selectorRef = new WeakReference<GrdIndexSelector>(selector);
        _effects.AddRange(effects);

        foreach (var effect in _effects)
            effect.PropertyChanged += OnEffectPropertyChanged;

        var firstPath = _effects.Count > 0 ? _effects[0].GradientFilePath : string.Empty;
        if (!string.IsNullOrEmpty(firstPath))
            selector.FilePath = firstPath;
    }

    public static GrdEffectPropertyBridge? TryCreate(GrdIndexSelector selector, object?[] items)
    {
        var effects = new List<GradientMapEffect>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] is GradientMapEffect effect)
                effects.Add(effect);
        }

        if (effects.Count == 0) return null;
        return new GrdEffectPropertyBridge(selector, effects);
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GradientMapEffect.GradientFilePath)) return;
        if (sender is not GradientMapEffect changed) return;

        if (!_selectorRef.TryGetTarget(out var selector))
        {
            Dispose();
            return;
        }

        selector.FilePath = changed.GradientFilePath;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var effect in _effects)
            effect.PropertyChanged -= OnEffectPropertyChanged;
        _effects.Clear();
    }
}
