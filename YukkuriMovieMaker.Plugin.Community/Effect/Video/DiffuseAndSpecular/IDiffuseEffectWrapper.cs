using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.DiffuseAndSpecular
{
    interface IDiffuseEffectWrapper : IEffectWrapper
    {
        float DiffuseConstant { get; set; }
        Vector3 Color { get; set; }
        float SurfaceScale { get; set; }
    }
}
