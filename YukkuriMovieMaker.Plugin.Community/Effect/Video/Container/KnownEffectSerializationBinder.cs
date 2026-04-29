using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

internal sealed class KnownEffectSerializationBinder : ISerializationBinder
{
    public static readonly KnownEffectSerializationBinder Instance = new();

    private KnownEffectSerializationBinder() { }

    public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = serializedType.Assembly.GetName().Name;
        typeName = serializedType.FullName;
    }

    public Type BindToType(string? assemblyName, string typeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var match = assemblies
            .Where(a => assemblyName == null || a.GetName().Name == assemblyName)
            .Select(a => a.GetType(typeName))
            .FirstOrDefault(t => t != null && typeof(IVideoEffect).IsAssignableFrom(t));

        return match ?? throw new InvalidOperationException(
            $"Type '{typeName}' is not a known IVideoEffect and cannot be deserialized.");
    }
}
