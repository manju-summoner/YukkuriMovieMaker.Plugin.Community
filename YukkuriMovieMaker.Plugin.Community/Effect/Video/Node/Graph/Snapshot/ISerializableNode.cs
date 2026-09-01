namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;

public interface ISerializableNode
{
    Dictionary<string, object?> SerializeCustomData();
    void DeserializeCustomData(Dictionary<string, object?> data);
}