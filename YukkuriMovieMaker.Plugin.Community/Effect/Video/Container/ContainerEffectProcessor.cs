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
        DisposeProcessors();
    }

    private void SynchronizeProcessors()
    {
        if (ReferenceEquals(_currentEffects, _effect.Effects)) return;

        var oldProcessors = new Dictionary<IVideoEffect, Stack<IVideoEffectProcessor>>();
        for (int i = 0; i < _currentEffects.Count; i++)
        {
            if (i < _processors.Count && _processors[i] != null)
            {
                var effect = _currentEffects[i];
                if (!oldProcessors.TryGetValue(effect, out var stack))
                {
                    stack = new Stack<IVideoEffectProcessor>();
                    oldProcessors[effect] = stack;
                }
                stack.Push(_processors[i]);
            }
        }

        var newProcessors = new List<IVideoEffectProcessor>(_effect.Effects.Count);
        for (int i = 0; i < _effect.Effects.Count; i++)
        {
            var effect = _effect.Effects[i];
            if (oldProcessors.TryGetValue(effect, out var stack) && stack.TryPop(out var processor))
            {
                newProcessors.Add(processor);
            }
            else
            {
                newProcessors.Add(effect.CreateVideoEffect(_devices));
            }
        }

        foreach (var stack in oldProcessors.Values)
        {
            foreach (var processor in stack)
            {
                processor.Dispose();
            }
        }

        _processors.Clear();
        _processors.AddRange(newProcessors);
        _currentEffects = _effect.Effects;
    }

    private void DisposeProcessors()
    {
        foreach (var processor in _processors)
        {
            processor.Dispose();
        }
        _processors.Clear();
    }
}
