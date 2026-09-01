namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

public abstract class Port
{
    protected Port(NodeLogic owner, Type valueType)
    {
        Owner = owner;
        ValueType = valueType;
    }

    public NodeLogic Owner { get; }
    public Type ValueType { get; }
}