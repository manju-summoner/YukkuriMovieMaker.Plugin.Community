using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal interface IModelParser
{
    string Id { get; }
    int Version { get; }
    IReadOnlyList<string> Extensions { get; }
    Model3DData Parse(string path);
}
