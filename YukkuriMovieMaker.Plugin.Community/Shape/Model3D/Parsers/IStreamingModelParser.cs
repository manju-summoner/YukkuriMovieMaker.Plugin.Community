using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Cache;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;

internal interface IStreamingModelParser : IModelParser
{
    Model3DData StreamToCache(string path, IStreamingCacheWriter cacheWriter);
}
