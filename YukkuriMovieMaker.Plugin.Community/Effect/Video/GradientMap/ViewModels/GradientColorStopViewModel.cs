using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Core;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Models;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.ViewModels;

public sealed class GradientColorStopViewModel : INotifyPropertyChanged
{
    public GradientColorStopViewModel(float position, Color color)
    {
        Position = Math.Clamp(position, 0f, 1f);
        Color = color;
    }

    public float Position
    {
        get;
        set
        {
            var v = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(field - v) < float.Epsilon) return;
            field = v;
            Raise();
        }
    }

    public Color Color
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Raise();
        }
    }

    public GradientColorStop ToModel() =>
        new(Position, Color.R, Color.G, Color.B, Color.A);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(n!));
}
