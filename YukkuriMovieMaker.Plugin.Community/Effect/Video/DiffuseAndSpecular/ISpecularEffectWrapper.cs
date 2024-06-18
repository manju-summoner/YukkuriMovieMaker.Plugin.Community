using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    interface ISpecularEffectWrapper : IEffectWrapper
    {
        float SpecularConstant { get; set; }
        float SpecularExponent { get; set; }
        Vector3 Color { get; set; }
        float SurfaceScale { get; set; }
    }
}
