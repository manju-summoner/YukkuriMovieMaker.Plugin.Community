namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

public record PortDefinition(
    string Name,
    Type ValueType,
    string Label = "",
    string Description = "",
    object? DefaultValue = null
);