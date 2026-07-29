using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering;

internal readonly record struct Model3DRenderState(
    Vector3 Position,
    Vector3 Rotation,
    float Scale,
    float FieldOfView,
    ProjectionType Projection,
    Vector4 BaseColor,
    Vector3 LightPosition,
    LightType LightType,
    bool IsLightEnabled);
