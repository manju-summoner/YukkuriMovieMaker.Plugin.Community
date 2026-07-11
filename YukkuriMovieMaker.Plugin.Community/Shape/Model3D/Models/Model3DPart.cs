using System.Numerics;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Models;

internal struct Model3DPart
{
    public const float DefaultMetallic = 0.0f;
    public const float DefaultRoughness = 1.0f;

    public string TexturePath;
    public string MetallicRoughnessTexturePath;
    public int IndexOffset;
    public int IndexCount;
    public Vector4 BaseColor;
    public float Metallic;
    public float Roughness;
    public float AlphaCutoff;
    public bool ForceTransparent;
    public bool IgnoreAlpha;
    public byte AddressU;
    public byte AddressV;
    public Vector3 Center;

    public Model3DPart()
    {
        TexturePath = string.Empty;
        MetallicRoughnessTexturePath = string.Empty;
        IndexOffset = 0;
        IndexCount = 0;
        BaseColor = Vector4.One;
        Metallic = DefaultMetallic;
        Roughness = DefaultRoughness;
        AlphaCutoff = 0.0f;
        ForceTransparent = false;
        IgnoreAlpha = false;
        AddressU = 0;
        AddressV = 0;
        Center = Vector3.Zero;
    }

    public readonly bool IsOpaque => !ForceTransparent && BaseColor.W >= 1.0f;
}
