using System.Reflection;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public static class Registry
{
    private static readonly Dictionary<string, Type> Nodes = new();
    public static IReadOnlyDictionary<string, Type> AllNodes => Nodes;

    public static void RegisterFromAssembly(Assembly asm)
    {
        foreach (var type in asm.GetTypes())
        {
            if (!typeof(NodeLogic).IsAssignableFrom(type)) continue;

            Nodes.Add(type.AssemblyQualifiedName ?? type.Name, type);
        }
    }

    public static void RegisterType(Type type)
    {
        if (!typeof(NodeLogic).IsAssignableFrom(type)) return;
        var key = type.AssemblyQualifiedName ?? type.Name;
        Nodes.TryAdd(key, type);
    }

    public static NodeLogic Create(string assemblyQualifiedName, Guid id)
    {
        var type = Nodes[assemblyQualifiedName];
        var node = (NodeLogic)Activator.CreateInstance(type)!;
        node.Id = id;
        return node;
    }
}