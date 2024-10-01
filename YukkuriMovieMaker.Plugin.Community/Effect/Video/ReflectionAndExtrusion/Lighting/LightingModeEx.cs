using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.DistantDiffuse;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.DistantSpecular;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.PointDiffuse;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting.PointSpecular;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Lighting
{
    internal static class LightingModeEx
    {
        public static LightingParameterBase Convert(this LightingMode mode, LightingParameterBase current)
        {
            var store = current.GetSharedData();
            LightingParameterBase param = mode switch
            {
                LightingMode.PointDiffuse => new PointDiffuseLightingParameter(store),
                LightingMode.DistantDiffuse => new DistantDiffuseLightingParameter(store),
                LightingMode.PointSpecular => new PointSpecularLightingParameter(store),
                LightingMode.DistantSpecular => new DistantSpecularLightingParameter(store),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            return param.GetType() != current.GetType() ? param : current;
        }
    }
}
