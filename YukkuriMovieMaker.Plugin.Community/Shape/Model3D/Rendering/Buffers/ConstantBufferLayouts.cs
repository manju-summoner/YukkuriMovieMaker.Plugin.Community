using System.Numerics;
using System.Runtime.InteropServices;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Rendering.Buffers;

[StructLayout(LayoutKind.Sequential, Size = 64)]
internal struct CBPerFrame
{
    public Vector4 CameraPosition;
    public Vector4 LightPosition;
    public Vector4 LightTarget;
    public float LightType;
    public float LightEnabled;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CBPerObject
{
    public Matrix4x4 WorldViewProjection;
    public Matrix4x4 World;
}

[StructLayout(LayoutKind.Sequential, Size = 48)]
internal struct CBPerMaterial
{
    public Vector4 BaseColor;
    public float Metallic;
    public float Roughness;
    public float AlphaCutoff;
    public float ForceOpaque;
    public float UiAlpha;
}
