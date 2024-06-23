using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.BevelHeightmap;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.HeightmapFile;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap
{
    internal static class HeightMapModeEx
    {
        public static HeightmapParameterBase Convert(this HeightmapMode mode, HeightmapParameterBase current)
        {
            HeightmapParameterBase param = mode switch
            {
                HeightmapMode.Bevel => new BevelHeightmapParameter(current.GetSharedData()),
                HeightmapMode.HeightmapFile => new HeightmapFileParameter(current.GetSharedData()),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            if (param.GetType() != current.GetType())
                return param;
            return current;
        }
    }
}
