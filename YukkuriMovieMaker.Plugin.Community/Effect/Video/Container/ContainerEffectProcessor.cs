using System.Collections.Immutable;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class ContainerEffectProcessor : IVideoEffectProcessor
{
    private readonly ContainerEffect _effect;
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly DisposeCollector _disposer = new();
    private readonly List<IVideoEffectProcessor> _processors = new();
    private ImmutableList<IVideoEffect> _currentEffects = ImmutableList<IVideoEffect>.Empty;
    private ID2D1Image? _inputImage;
    private bool _disposed;

    public ID2D1Image Output { get; private set; } = null!;

    public ContainerEffectProcessor(ContainerEffect effect, IGraphicsDevicesAndContext devices)
    {
        _effect = effect;
        _devices = devices;
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        if (_disposed || !_effect.IsEnabled)
        {
            Output = _inputImage!;
            return effectDescription.DrawDescription;
        }

        SynchronizeProcessors();

        if (_processors.Count == 0)
        {
            Output = _inputImage!;
            return effectDescription.DrawDescription;
        }

        ID2D1Image? current = _inputImage;
        var description = effectDescription;
        var activeEffects = _currentEffects;
        for (int i = 0; i < _processors.Count; i++)
        {
            if (i >= activeEffects.Count || !activeEffects[i].IsEnabled) continue;
            _processors[i].SetInput(current);
            var updated = _processors[i].Update(description);
            current = _processors[i].Output;
            description = description with { DrawDescription = updated };
        }

        Output = current!;
        return description.DrawDescription;
    }

    public void SetInput(ID2D1Image? input)
    {
        if (!_disposed) _inputImage = input;
    }

    public void ClearInput()
    {
        if (!_disposed) _inputImage = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposer.Dispose();
        _processors.Clear();
    }

    private void SynchronizeProcessors()
    {
        var nextEffects = _effect.GetSelectedTabEffects();
        if (ReferenceEquals(_currentEffects, nextEffects)) return;

        var oldProcessors = new Dictionary<IVideoEffect, IVideoEffectProcessor>(_currentEffects.Count);
        for (int i = 0; i < _currentEffects.Count && i < _processors.Count; i++)
        {
            if (!oldProcessors.TryAdd(_currentEffects[i], _processors[i]))
                throw new InvalidOperationException("Same IVideoEffect instance appears multiple times in the previous effects list.");
        }

        var seen = new HashSet<IVideoEffect>(nextEffects.Count);
        var newProcessors = new List<IVideoEffectProcessor>(nextEffects.Count);
        foreach (var effect in nextEffects)
        {
            if (!seen.Add(effect))
                throw new InvalidOperationException("Same IVideoEffect instance appears multiple times in the current effects list.");

            if (oldProcessors.Remove(effect, out var processor))
            {
                newProcessors.Add(processor);
            }
            else
            {
                var created = effect.CreateVideoEffect(_devices);
                _disposer.Collect(created);
                newProcessors.Add(created);
            }
        }

        // 再利用されなかった旧プロセッサは Disposer から外して破棄
        foreach (var processor in oldProcessors.Values)
        {
            var p = processor;
            _disposer.RemoveAndDispose(ref p);
        }

        _processors.Clear();
        _processors.AddRange(newProcessors);
        _currentEffects = nextEffects;
    }
}
