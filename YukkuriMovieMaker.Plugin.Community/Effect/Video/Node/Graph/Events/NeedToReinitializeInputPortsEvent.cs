using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;

public class NeedToReinitializeInputPortsEvent : EventArgs
{
    public NeedToReinitializeInputPortsEvent(string propName, InputsContainer newContainer)
    {
        PropName = propName;
        NewContainer = newContainer;
    }

    public string PropName { get; }
    public InputsContainer NewContainer { get; }
}