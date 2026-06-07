using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

public abstract class InputsContainer
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected internal void Set<T>(ref T field, T value, [CallerMemberName] string name = null!)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}