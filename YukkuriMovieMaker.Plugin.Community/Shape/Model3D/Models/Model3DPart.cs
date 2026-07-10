using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

internal struct Model3DPart
{
    public const float DefaultMetallic = 0.0f;
    public const float DefaultRoughness = 1.0f;

    public string TexturePath;
    public int IndexOffset;
    public int IndexCount;
    public Vector4 BaseColor;
    public float Metallic;
    public float Roughness;
    public Vector3 Center;

    public Model3DPart()
    {
        TexturePath = string.Empty;
        IndexOffset = 0;
        IndexCount = 0;
        BaseColor = Vector4.One;
        Metallic = DefaultMetallic;
        Roughness = DefaultRoughness;
        Center = Vector3.Zero;
    }

    public readonly bool IsOpaque => BaseColor.W >= 1.0f;
}
